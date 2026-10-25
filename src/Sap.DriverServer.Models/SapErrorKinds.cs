namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Provides the SAP-specific error kinds reported in <see cref="WebDriverErrorModel.Data"/>.
    /// </summary>
    /// <remarks>
    /// The W3C error code tells a client how to react in general; the SAP error kind tells an operator which
    /// SAP prerequisite or runtime condition caused the failure. Values are stable, camel-cased identifiers.
    /// </remarks>
    public static class SapErrorKinds
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies that Windows denied access to the SAP GUI COM objects (for example a different user or integrity level).
        /// </summary>
        public const string AccessDenied = "accessDenied";

        /// <summary>
        /// Specifies that more than one SAP GUI session matched the requested selection.
        /// </summary>
        public const string AmbiguousSession = "ambiguousSession";

        /// <summary>
        /// Specifies that a COM call failed for a reason that has no more specific kind.
        /// </summary>
        public const string ComFailure = "comFailure";

        /// <summary>
        /// Specifies the <see cref="WebDriverErrorModel.Data"/> key that carries the SAP error kind.
        /// </summary>
        public const string DataKey = "sapErrorKind";

        /// <summary>
        /// Specifies that the SAP GUI Scripting object model does not expose the requested property, method, or control.
        /// </summary>
        public const string NotExposedByScripting = "notExposedByScripting";

        /// <summary>
        /// Specifies that no open SAP GUI session matched the requested selection.
        /// </summary>
        public const string NoMatchingSession = "noMatchingSession";

        /// <summary>
        /// Specifies that no SAP GUI (SAP Logon) instance of this Windows user and integrity level is registered in the
        /// running object table, or it exited.
        /// </summary>
        public const string SapGuiNotRunning = "sapGuiNotRunning";

        /// <summary>
        /// Specifies that SAP GUI Scripting is disabled in the SAP GUI client options.
        /// </summary>
        public const string ScriptingDisabled = "scriptingDisabled";

        /// <summary>
        /// Specifies that the SAP application server disabled scripting (profile parameter <c>sapgui/user_scripting</c>).
        /// </summary>
        public const string ScriptingDisabledByServer = "scriptingDisabledByServer";

        /// <summary>
        /// Specifies that the SAP GUI session stayed busy beyond the configured wait or rejected the call.
        /// </summary>
        public const string SessionBusy = "sessionBusy";

        /// <summary>
        /// Specifies that a COM call did not return within the configured time budget.
        /// </summary>
        public const string Timeout = "timeout";
        #endregion
    }
}
