using System.Collections.Generic;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the <c>value</c> payload returned by the "New Session" endpoint.
    /// </summary>
    public class SessionResponseModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the capabilities the session was created with, including the resolved
        /// <see cref="SapCapabilities.Session"/> entry.
        /// </summary>
        public IDictionary<string, object> Capabilities { get; set; }

        /// <summary>
        /// Gets or sets the WebDriver session identifier.
        /// </summary>
        public string SessionId { get; set; }
        #endregion
    }
}
