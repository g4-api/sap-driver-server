using System;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Represents the connection to the SAP GUI Scripting engine (<c>GuiApplication</c>).
    /// </summary>
    /// <remarks>
    /// Every member is called only on the STA dispatcher thread, through <see cref="SapScriptingHost"/>.
    /// The engine owns two kinds of COM references: the long-lived application object, kept until
    /// <see cref="Reset"/> or <see cref="IDisposable.Dispose"/>, and the per-operation objects created while an
    /// operation walks the tree, released by <see cref="ReleaseOperationObjects"/> after each operation.
    /// </remarks>
    public interface ISapGuiEngine : IDisposable
    {
        #region *** Methods      ***
        /// <summary>
        /// Gets the scripting application object, attaching to the running SAP GUI on first use.
        /// </summary>
        /// <returns>The <c>GuiApplication</c> node.</returns>
        /// <exception cref="SapDriverException">
        /// SAP GUI is not running for this Windows user and integrity level, access is denied, or scripting is disabled.
        /// </exception>
        ISapGuiNode GetApplication();

        /// <summary>
        /// Releases the COM references created since the previous call, keeping the application object.
        /// </summary>
        void ReleaseOperationObjects();

        /// <summary>
        /// Drops the cached application object so the next <see cref="GetApplication"/> call attaches again.
        /// </summary>
        /// <remarks>
        /// Called after SAP GUI disconnects (for example SAP Logon was closed and reopened).
        /// </remarks>
        void Reset();
        #endregion
    }
}
