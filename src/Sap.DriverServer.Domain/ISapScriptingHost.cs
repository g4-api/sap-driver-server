using System;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Runs domain operations against SAP GUI Scripting on the STA dispatcher thread.
    /// </summary>
    /// <remarks>
    /// The host is the only entry point repositories use to reach SAP GUI. It attaches to the scripting engine on
    /// demand, serializes operations, enforces the COM call timeout, converts COM failures into
    /// <see cref="SapDriverException"/> instances, and releases every COM reference created by an operation once it ends.
    /// Nodes passed to an operation are valid only inside that operation; do not store them.
    /// </remarks>
    public interface ISapScriptingHost : IDisposable
    {
        #region *** Methods      ***
        /// <summary>
        /// Runs an action that receives the <c>GuiApplication</c> node.
        /// </summary>
        /// <param name="operation">The action to run on the dispatcher thread.</param>
        /// <exception cref="SapDriverException">The engine is unavailable, a COM call failed, or the operation timed out.</exception>
        void Invoke(Action<ISapGuiNode> operation);

        /// <summary>
        /// Runs a function that receives the <c>GuiApplication</c> node and returns a COM-free result.
        /// </summary>
        /// <typeparam name="T">The result type; it must not contain <see cref="ISapGuiNode"/> references.</typeparam>
        /// <param name="operation">The function to run on the dispatcher thread.</param>
        /// <returns>The function result.</returns>
        /// <exception cref="SapDriverException">The engine is unavailable, a COM call failed, or the operation timed out.</exception>
        T Invoke<T>(Func<ISapGuiNode, T> operation);
        #endregion
    }
}
