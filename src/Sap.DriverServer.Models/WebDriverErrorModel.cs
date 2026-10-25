using System.Collections.Generic;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the W3C WebDriver error object placed in the <c>value</c> member of an error response.
    /// </summary>
    /// <remarks>
    /// The shape matches the UIA driver server. SAP-specific diagnostics travel in <see cref="Data"/> under
    /// the <see cref="SapErrorKinds.DataKey"/> key so existing clients keep parsing <see cref="Error"/> unchanged.
    /// </remarks>
    public class WebDriverErrorModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets additional structured error data, such as the SAP error kind or candidate session identifiers.
        /// </summary>
        /// <remarks>
        /// Null when the error has no additional data; the member is then omitted from the JSON response.
        /// </remarks>
        public Dictionary<string, object> Data { get; set; }

        /// <summary>
        /// Gets or sets the W3C error code, one of the <see cref="WebDriverErrorCodes"/> values.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Gets or sets the human-readable error message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the stack trace.
        /// </summary>
        /// <remarks>
        /// The SAP driver leaves this member empty for SAP and COM failures so internal details stay in the server log.
        /// </remarks>
        public string StackTrace { get; set; }
        #endregion
    }
}
