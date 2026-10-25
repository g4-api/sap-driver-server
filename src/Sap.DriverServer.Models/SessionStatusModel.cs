using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the <c>value</c> payload returned by the W3C "Status" endpoint.
    /// </summary>
    /// <remarks>
    /// Every member except <see cref="Sap"/> matches the UIA driver server. <see cref="Sap"/> is an additive,
    /// backward-compatible member that reports whether SAP GUI Scripting is currently reachable.
    /// </remarks>
    public class SessionStatusModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the human-readable readiness message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the operating system description (architecture, name, version).
        /// </summary>
        [JsonPropertyName("os")]
        public IDictionary<string, object> OperatingSystem { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the server accepts a new session.
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>
        /// Gets or sets the SAP GUI Scripting reachability report.
        /// </summary>
        public SapScriptingStatusModel Sap { get; set; }

        /// <summary>
        /// Gets or sets the identifiers of the active WebDriver sessions.
        /// </summary>
        public IEnumerable<string> Sessions { get; set; }
        #endregion
    }
}
