using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Scripting
{
    /// <summary>
    /// Late-bound <see cref="ISapGuiNode"/> over one SAP GUI Scripting COM object.
    /// </summary>
    /// <remarks>
    /// Calls go through <c>IDispatch</c> using <see cref="Type.InvokeMember(string, BindingFlags, Binder, object, object[], CultureInfo)"/>.
    /// Every COM object returned by a call is registered with the owning <see cref="ComSapGuiEngine"/>, which
    /// releases it when the current operation ends; the node must not be used after that.
    /// </remarks>
    internal sealed class ComSapGuiNode : ISapGuiNode
    {
        #region *** Fields       ***
        // Holds the wrapped RCW; its lifetime is owned by the engine, not by this node.
        private readonly object _comObject;

        // Registers COM objects returned by calls so the operation's cleanup releases them.
        private readonly ComSapGuiEngine _engine;

        // Caches the identifier after the first read to avoid repeated cross-process calls.
        private string _id;

        // Caches the type after the first read to avoid repeated cross-process calls.
        private string _type;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ComSapGuiNode"/> class.
        /// </summary>
        /// <param name="engine">The engine that owns the lifetime of returned COM objects.</param>
        /// <param name="comObject">The SAP GUI Scripting COM object to wrap.</param>
        public ComSapGuiNode(ComSapGuiEngine engine, object comObject)
        {
            // Keep both references; the engine decides when the wrapped RCW is released.
            _engine = engine;
            _comObject = comObject;
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public string Id => _id ??= Convert.ToString(GetProperty(nameof(Id)), CultureInfo.InvariantCulture);

        /// <inheritdoc />
        public string Type => _type ??= Convert.ToString(GetProperty(nameof(Type)), CultureInfo.InvariantCulture);
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public ISapGuiNode FindById(string id)
        {
            // Pass raiseError=false so a missing control returns null instead of raising a COM error.
            var result = InvokeMember(_comObject, "FindById", [id, false]);

            return _engine.NewNode(result);
        }

        /// <inheritdoc />
        public IReadOnlyList<ISapGuiNode> GetChildren()
        {
            // Treat objects without a Children collection as leaves.
            object collection;
            try
            {
                collection = GetMember(_comObject, "Children");
            }
            catch (SapDriverException exception) when (exception.ErrorKind == SapErrorKinds.NotExposedByScripting)
            {
                return [];
            }

            // Track the collection RCW itself; it is a COM object distinct from its items.
            _engine.Track(collection);
            if (collection is null)
            {
                return [];
            }

            // Read the count once and materialize each item through ElementAt, which every SAP collection exposes.
            var count = Convert.ToInt32(GetMember(collection, "Count"), CultureInfo.InvariantCulture);
            var children = new List<ISapGuiNode>(count);

            for (var index = 0; index < count; index++)
            {
                var child = _engine.NewNode(InvokeMember(collection, "ElementAt", [index]));
                if (child is not null)
                {
                    children.Add(child);
                }
            }

            return children;
        }

        /// <inheritdoc />
        public ISapGuiNode GetNode(string propertyName)
        {
            // Wrap the object-valued property; scalar or empty values produce null.
            return _engine.NewNode(GetMember(_comObject, propertyName));
        }

        /// <inheritdoc />
        public object GetProperty(string propertyName)
        {
            // Read the property and track any COM object it returns so the operation releases it.
            var value = GetMember(_comObject, propertyName);
            _engine.Track(value);

            return value;
        }

        /// <inheritdoc />
        public IReadOnlyList<object> InvokeCollectionMethod(string methodName, params object[] arguments)
        {
            // Invoke the method and track the returned GuiCollection so the operation releases it.
            var collection = InvokeMember(_comObject, methodName, arguments ?? []);
            _engine.Track(collection);
            if (collection is null)
            {
                return [];
            }

            // Read every item through elementAt; scripting objects are wrapped, scalars are returned as they are.
            var count = Convert.ToInt32(GetMember(collection, "Count"), CultureInfo.InvariantCulture);
            var items = new List<object>(count);

            for (var index = 0; index < count; index++)
            {
                var item = InvokeMember(collection, "ElementAt", [index]);
                items.Add((object)_engine.NewNode(item) ?? item);
            }

            return items;
        }

        /// <inheritdoc />
        public object InvokeMethod(string methodName, params object[] arguments)
        {
            // Invoke the method and track any COM object it returns so the operation releases it.
            var result = InvokeMember(_comObject, methodName, arguments ?? []);
            _engine.Track(result);

            return result;
        }

        /// <inheritdoc />
        public void SetProperty(string propertyName, object value)
        {
            // Assign the property through IDispatch property-put.
            SetMember(_comObject, propertyName, value);
        }

        // Reads a property through IDispatch property-get; see InvokeDispatch for the failure contract.
        internal static object GetMember(object target, string name)
        {
            // Dispatch the property-get by name without arguments.
            return InvokeDispatch(name, () => target.GetType().InvokeMember(name, BindingFlags.GetProperty, binder: null, target, args: [], CultureInfo.InvariantCulture));
        }

        // Invokes a method through IDispatch; see InvokeDispatch for the failure contract.
        internal static object InvokeMember(object target, string name, object[] arguments)
        {
            // Dispatch the method call by name with positional arguments.
            return InvokeDispatch(name, () => target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, binder: null, target, arguments, CultureInfo.InvariantCulture));
        }

        // Assigns a property through IDispatch property-put; see InvokeDispatch for the failure contract.
        internal static void SetMember(object target, string name, object value)
        {
            // Dispatch the property-put by name with the single value argument.
            InvokeDispatch(name, () => target.GetType().InvokeMember(name, BindingFlags.SetProperty, binder: null, target, args: [value], CultureInfo.InvariantCulture));
        }

        // Runs one late-bound IDispatch call and unwraps reflection's TargetInvocationException.
        // Absent members become NotExposedByScripting failures; every other COM failure propagates as COMException
        // so SapScriptingHost classifies it. The call has no side effects beyond the SAP member it invokes.
        private static object InvokeDispatch(string name, Func<object> call)
        {
            try
            {
                // Run the dispatch call; SAP GUI Scripting member names are case-insensitive.
                return call();
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                // Surface the real failure instead of the reflection wrapper.
                ThrowInnerException(name, exception.InnerException);
                throw;
            }
            catch (COMException exception) when (SapErrorMapper.TestMemberNotExposed(exception.HResult))
            {
                // Report the absent member with its name, which is safe to return to the client.
                throw NewNotExposedException(name, exception);
            }
        }

        // Builds the client-facing failure for a member the scripting object does not expose.
        private static SapDriverException NewNotExposedException(string name, COMException exception)
        {
            // Name only the member; the COM message is kept as the inner exception for the log.
            var message = $"SAP GUI Scripting does not expose '{name}' on this object.";

            return new SapDriverException(
                WebDriverErrorCodes.UnsupportedOperation,
                SapErrorKinds.NotExposedByScripting,
                StatusCodes.Status500InternalServerError,
                message,
                data: null,
                innerException: exception);
        }

        // Rethrows the unwrapped failure of a late-bound call, preserving its original stack trace.
        private static void ThrowInnerException(string name, Exception innerException)
        {
            // Convert absent-member failures into the classified driver failure.
            if (innerException is COMException comException && SapErrorMapper.TestMemberNotExposed(comException.HResult))
            {
                throw NewNotExposedException(name, comException);
            }

            // Rethrow every other failure unchanged for classification by the scripting host.
            ExceptionDispatchInfo.Capture(innerException).Throw();
        }
        #endregion
    }
}
