/*
 * RESOURCES
 * https://help.sap.com/docs/sap_gui_for_windows/b47d018c3b9b45e897faf66a6c0885a8/a2e9357389334dc89eecc1fb13999ee3.html
 */
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Scripting
{
    /// <summary>
    /// Late-bound <see cref="ISapGuiEngine"/> that attaches to the running SAP GUI for Windows through its ROT entry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attachment reads the <c>SAPGUI</c> entry that SAP Logon registers in the Windows running object table and calls
    /// <c>GetScriptingEngine</c> on it. The table is read directly through <see cref="IRunningObjectTableReader"/>
    /// instead of the <c>SapROTWr</c> component, so no SAP registry entry is needed and the process bitness does
    /// not have to match SAP GUI: calls cross the process boundary through standard <c>IDispatch</c> marshaling.
    /// Late binding keeps the driver buildable and testable on machines without SAP GUI.
    /// </para>
    /// <para>
    /// The ROT exposes one <c>SAPGUI</c> entry, registered by the first SAP Logon process. The driver therefore
    /// always attaches to that instance; other SAP GUI processes are not reachable through scripting.
    /// </para>
    /// <para>
    /// Every member runs on the dispatcher thread. Per-operation COM objects are tracked and released by
    /// <see cref="ReleaseOperationObjects"/>; the application object is released by <see cref="Reset"/> or
    /// <see cref="Dispose"/>. References are released with <see cref="Marshal.ReleaseComObject(object)"/>, never
    /// <see cref="Marshal.FinalReleaseComObject(object)"/>, so an RCW that is still in use elsewhere stays valid.
    /// </para>
    /// </remarks>
    public sealed class ComSapGuiEngine : ISapGuiEngine
    {
        #region *** Constants    ***
        // Names the running object table entry that SAP Logon registers for scripting clients.
        private const string RotEntryName = "SAPGUI";
        #endregion

        #region *** Fields       ***
        // Tracks COM objects created during the current operation so they are released when it ends.
        private readonly List<object> _operationObjects = [];

        // Reads the running object table; replaced by a fake in tests.
        private readonly IRunningObjectTableReader _tableReader;

        // Holds the GuiApplication RCW for the engine lifetime, or null before attachment and after reset.
        private object _application;

        // Wraps the cached application RCW; recreated on the next attachment after a reset.
        private ComSapGuiNode _applicationNode;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ComSapGuiEngine"/> class that reads the Windows running object table.
        /// </summary>
        public ComSapGuiEngine()
            : this(tableReader: new RunningObjectTableReader())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ComSapGuiEngine"/> class with an explicit table reader.
        /// </summary>
        /// <param name="tableReader">The running object table reader used to find SAP Logon.</param>
        public ComSapGuiEngine(IRunningObjectTableReader tableReader)
        {
            // Require the reader; attachment has no other way to find SAP Logon.
            ArgumentNullException.ThrowIfNull(argument: tableReader);

            _tableReader = tableReader;
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // Release the per-operation objects and the cached application object.
            Reset();
        }

        /// <inheritdoc />
        public ISapGuiNode GetApplication()
        {
            // Reuse the attached application; attaching per request adds several cross-process calls.
            if (_applicationNode is not null)
            {
                return _applicationNode;
            }

            // Attach once and wrap the application without tracking, because it outlives the operation.
            _application = NewApplication();
            _applicationNode = new ComSapGuiNode(engine: this, comObject: _application);

            return _applicationNode;
        }

        /// <inheritdoc />
        public void ReleaseOperationObjects()
        {
            // Release each tracked RCW once, balancing the reference taken when COM returned it to managed code.
            foreach (var comObject in _operationObjects)
            {
                ReleaseComObject(comObject);
            }

            _operationObjects.Clear();
        }

        /// <inheritdoc />
        public void Reset()
        {
            // Release outstanding operation objects before the application they belong to.
            ReleaseOperationObjects();

            // Drop the cached application so the next operation attaches again.
            ReleaseComObject(_application);
            _application = null;
            _applicationNode = null;
        }

        // Wraps a COM object returned during an operation and tracks it for release at the end of the operation.
        // Returns null for null or non-COM values so callers can treat "no object" uniformly.
        internal ComSapGuiNode NewNode(object comObject)
        {
            // Treat empty and scalar values as "no node".
            var isComObject = comObject is not null && Marshal.IsComObject(comObject);
            if (!isComObject)
            {
                return null;
            }

            // Track the RCW so ReleaseOperationObjects balances the reference.
            Track(comObject);

            return new ComSapGuiNode(engine: this, comObject);
        }

        // Records a COM object returned during the current operation for release when the operation ends.
        internal void Track(object comObject)
        {
            // Ignore scalar values; only RCWs hold COM references.
            if (comObject is not null && Marshal.IsComObject(comObject))
            {
                _operationObjects.Add(comObject);
            }
        }

        // Requests the GuiApplication object from the ROT entry, classifying a refusal as disabled scripting.
        private static object GetScriptingEngine(object rotEntry)
        {
            try
            {
                // Request the engine; a null result is treated like a refusal.
                var engine = ComSapGuiNode.InvokeMember(rotEntry, "GetScriptingEngine", [])
                    ?? throw NewScriptingDisabledException(innerException: null);
                
                return engine;
            }
            catch (COMException exception) when (exception.HResult == SapErrorMapper.AccessDenied)
            {
                // Report access problems, such as a server running as a different user than SAP Logon.
                throw NewAccessDeniedException(exception);
            }
            catch (COMException exception)
            {
                // Report every other refusal as client-side disabled scripting, the documented cause.
                throw NewScriptingDisabledException(innerException: exception);
            }
        }

        // Builds the access-denied failure, which names the user and integrity level rule for COM between processes.
        private static SapDriverException NewAccessDeniedException(COMException exception)
        {
            // Point at the rule the operator controls: run as the same user and elevation as SAP Logon.
            var message = "Access to SAP GUI Scripting was denied. Run this process as the same Windows user " +
                "and integrity level (elevated or not) as SAP Logon.";

            return SapDriverException.NewSessionNotCreated(SapErrorKinds.AccessDenied, message, innerException: exception);
        }

        // Builds the client-side "scripting disabled" failure with the remediation path in SAP GUI options.
        private static SapDriverException NewScriptingDisabledException(Exception innerException)
        {
            // Name the SAP GUI option that the user or administrator controls; the driver never changes it.
            var message = "SAP GUI Scripting is disabled. Enable it in SAP GUI Options > Accessibility & " +
                "Scripting > Scripting, or ask your SAP administrator.";

            return SapDriverException.NewSessionNotCreated(SapErrorKinds.ScriptingDisabled, message, innerException);
        }

        // Releases one RCW reference, ignoring null and non-COM values and RCWs already detached from their object.
        private static void ReleaseComObject(object comObject)
        {
            // Skip values that hold no COM reference.
            if (comObject is null || !Marshal.IsComObject(comObject))
            {
                return;
            }

            try
            {
                // Decrement this RCW's reference count; the underlying object stays alive for other holders.
                Marshal.ReleaseComObject(comObject);
            }
            catch (InvalidComObjectException)
            {
                // The RCW was already separated from its object; there is nothing left to release.
            }
        }

        // Reads the SAPGUI running object table entry, classifying a missing entry as sapGuiNotRunning.
        // The table shows only entries of the same user and integrity level, so the message names both.
        private object GetRotEntry()
        {
            const string message = "SAP GUI is not running for this Windows user. Start SAP Logon, as the same user " +
                "and integrity level (elevated or not) as this process, and open a connection.";

            try
            {
                // Look the entry up directly; SAP Logon registers it while it runs.
                var rotEntry = _tableReader.GetObject(RotEntryName)
                    ?? throw SapDriverException.NewSessionNotCreated(SapErrorKinds.SapGuiNotRunning, message);
                
                return rotEntry;
            }
            catch (COMException exception) when (exception.HResult == SapErrorMapper.AccessDenied)
            {
                // Classify access problems separately; they need a different remedy than starting SAP Logon.
                throw NewAccessDeniedException(exception);
            }
            catch (COMException exception)
            {
                // Every other table failure means the entry is not reachable.
                throw SapDriverException.NewSessionNotCreated(SapErrorKinds.SapGuiNotRunning, message, innerException: exception);
            }
        }

        // Finds SAP Logon in the running object table and requests its scripting engine.
        // The temporary ROT entry object is released in every outcome; only the engine is returned.
        private object NewApplication()
        {
            object rotEntry = null;

            try
            {
                // Read the SAPGUI entry, then request the engine; SAP GUI refuses it when scripting is disabled.
                rotEntry = GetRotEntry();
                return GetScriptingEngine(rotEntry);
            }
            finally
            {
                // Release the temporary entry; the engine keeps its own reference to SAP GUI.
                ReleaseComObject(rotEntry);
            }
        }
        #endregion
    }
}
