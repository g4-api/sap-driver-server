namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the <c>sap:options</c> capability that selects which open SAP GUI session a WebDriver session attaches to.
    /// </summary>
    /// <remarks>
    /// Every supplied criterion must match (logical AND). When no criterion is supplied the server attaches only
    /// if exactly one scriptable SAP GUI session is open. Any selection that matches more than one session fails
    /// with <see cref="SapErrorKinds.AmbiguousSession"/>; the server never picks one arbitrarily.
    /// The driver never opens connections, logs on, or handles credentials.
    /// </remarks>
    public class SapOptionsModel
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the sentinel value meaning "index not specified" for <see cref="ConnectionIndex"/> and <see cref="SessionIndex"/>.
        /// </summary>
        public const int NoIndex = -1;
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the SAP client number to match, such as <c>100</c>; null or empty means "any client".
        /// </summary>
        public string Client { get; set; }

        /// <summary>
        /// Gets or sets the SAP Logon connection description to match exactly (case-insensitive); null or empty means "any connection".
        /// </summary>
        public string ConnectionDescription { get; set; }

        /// <summary>
        /// Gets or sets the zero-based connection index (<c>con[n]</c>) to match; <see cref="NoIndex"/> means "any connection".
        /// </summary>
        public int ConnectionIndex { get; set; } = NoIndex;

        /// <summary>
        /// Gets or sets the zero-based session index within its connection (<c>ses[n]</c>); <see cref="NoIndex"/> means "any session".
        /// </summary>
        public int SessionIndex { get; set; } = NoIndex;

        /// <summary>
        /// Gets or sets the SAP GUI Scripting session identifier, such as <c>/app/con[0]/ses[0]</c>; the leading
        /// <c>/app/</c> segment is optional. Null or empty means "any session".
        /// </summary>
        public string SessionPath { get; set; }

        /// <summary>
        /// Gets or sets the SAP system identifier (SID) to match, such as <c>DEV</c>; null or empty means "any system".
        /// </summary>
        public string SystemName { get; set; }
        #endregion
    }
}
