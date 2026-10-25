using System.Collections.Generic;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Represents one SAP GUI Scripting object (application, connection, session, window, or control).
    /// </summary>
    /// <remarks>
    /// The interface is the only surface the domain layer uses to talk to SAP GUI. The production implementation
    /// wraps a late-bound COM object and is valid only on the STA dispatcher thread for the duration of the
    /// operation that produced it; the test implementation is an in-memory tree. Implementations throw a
    /// <see cref="SapDriverException"/> with <see cref="Models.SapErrorKinds.NotExposedByScripting"/> when the
    /// object does not expose a requested property or method; every other failure propagates unchanged and is
    /// converted by <see cref="SapScriptingHost"/>.
    /// </remarks>
    public interface ISapGuiNode
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the absolute SAP GUI Scripting identifier, such as <c>/app/con[0]/ses[0]/wnd[0]/usr/btnOK</c>.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the SAP GUI Scripting type name, such as <c>GuiButton</c>.
        /// </summary>
        string Type { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Resolves a descendant by absolute or relative SAP GUI Scripting identifier.
        /// </summary>
        /// <param name="id">The identifier passed to <c>findById</c>.</param>
        /// <returns>The resolved node, or null when no object has that identifier.</returns>
        ISapGuiNode FindById(string id);

        /// <summary>
        /// Gets the direct children exposed through the <c>Children</c> collection.
        /// </summary>
        /// <returns>The children in SAP order; an empty list when the object is not a container.</returns>
        IReadOnlyList<ISapGuiNode> GetChildren();

        /// <summary>
        /// Gets a property whose value is another scripting object, such as <c>ActiveWindow</c> or <c>Info</c>.
        /// </summary>
        /// <param name="propertyName">The SAP GUI Scripting property name.</param>
        /// <returns>The wrapped object, or null when the property value is empty.</returns>
        ISapGuiNode GetNode(string propertyName);

        /// <summary>
        /// Gets a scalar property value (string, number, or Boolean).
        /// </summary>
        /// <param name="propertyName">The SAP GUI Scripting property name.</param>
        /// <returns>The raw property value, which may be null.</returns>
        object GetProperty(string propertyName);

        /// <summary>
        /// Invokes a scripting method that returns a <c>GuiCollection</c>, such as <c>findByPosition</c>, and reads its items.
        /// </summary>
        /// <param name="methodName">The SAP GUI Scripting method name.</param>
        /// <param name="arguments">The positional method arguments.</param>
        /// <returns>
        /// The collection items in order: scalars as raw values, scripting objects as <see cref="ISapGuiNode"/>;
        /// an empty list when the method returns no collection.
        /// </returns>
        IReadOnlyList<object> InvokeCollectionMethod(string methodName, params object[] arguments);

        /// <summary>
        /// Invokes a scripting method, such as <c>press</c>, <c>select</c>, or <c>sendVKey</c>.
        /// </summary>
        /// <param name="methodName">The SAP GUI Scripting method name.</param>
        /// <param name="arguments">The positional method arguments.</param>
        /// <returns>The raw method result, which may be null.</returns>
        object InvokeMethod(string methodName, params object[] arguments);

        /// <summary>
        /// Sets a scalar property value, such as <c>text</c>, <c>selected</c>, or <c>key</c>.
        /// </summary>
        /// <param name="propertyName">The SAP GUI Scripting property name.</param>
        /// <param name="value">The value to assign.</param>
        void SetProperty(string propertyName, object value);
        #endregion
    }
}
