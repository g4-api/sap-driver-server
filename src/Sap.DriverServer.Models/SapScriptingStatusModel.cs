namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the SAP GUI Scripting reachability report returned by the status endpoint.
    /// </summary>
    public class SapScriptingStatusModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets a value indicating whether the scripting engine was reachable when the status was requested.
        /// </summary>
        public bool Available { get; set; }

        /// <summary>
        /// Gets or sets the number of open connections visible to scripting.
        /// </summary>
        public int Connections { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="SapErrorKinds"/> value explaining why scripting is unavailable.
        /// </summary>
        /// <remarks>
        /// Null when <see cref="Available"/> is true; the member is then omitted from the JSON response.
        /// </remarks>
        public string ErrorKind { get; set; }

        /// <summary>
        /// Gets or sets the operator-facing explanation that accompanies <see cref="ErrorKind"/>.
        /// </summary>
        public string Message { get; set; }
        #endregion
    }
}
