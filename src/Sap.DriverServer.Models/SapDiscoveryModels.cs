using System.Collections.Generic;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents one SAP GUI connection discovered through the scripting engine.
    /// </summary>
    /// <remarks>
    /// Discovery models carry identifiers and system metadata only. They never include the logged-on user,
    /// the current transaction, or screen contents, so they are safe to return and log.
    /// </remarks>
    public class SapConnectionInfoModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the SAP Logon entry description used to open the connection.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the SAP GUI Scripting identifier, such as <c>/app/con[0]</c>.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the zero-based connection index parsed from <see cref="Id"/>.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the application server disabled scripting for this connection.
        /// </summary>
        /// <remarks>
        /// When true SAP GUI hides the connection's sessions from scripting, so <see cref="Sessions"/> is empty.
        /// </remarks>
        public bool ScriptingDisabledByServer { get; set; }

        /// <summary>
        /// Gets or sets the scriptable sessions of the connection.
        /// </summary>
        public IList<SapSessionInfoModel> Sessions { get; set; } = [];
        #endregion
    }

    /// <summary>
    /// Represents one SAP GUI session discovered through the scripting engine.
    /// </summary>
    public class SapSessionInfoModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets a value indicating whether the session was processing a server round trip when discovered.
        /// </summary>
        public bool Busy { get; set; }

        /// <summary>
        /// Gets or sets the SAP client number, such as <c>100</c>.
        /// </summary>
        public string Client { get; set; }

        /// <summary>
        /// Gets or sets the owning connection's SAP Logon description.
        /// </summary>
        public string ConnectionDescription { get; set; }

        /// <summary>
        /// Gets or sets the zero-based index of the owning connection.
        /// </summary>
        public int ConnectionIndex { get; set; }

        /// <summary>
        /// Gets or sets the SAP GUI Scripting identifier, such as <c>/app/con[0]/ses[0]</c>.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the zero-based session index within the owning connection.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the SAP system identifier (SID).
        /// </summary>
        public string SystemName { get; set; }
        #endregion
    }
}
