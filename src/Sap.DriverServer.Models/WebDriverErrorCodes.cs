/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#errors
 */
namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Provides the W3C WebDriver error codes used by the SAP driver server.
    /// </summary>
    public static class WebDriverErrorCodes
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies that the target control exists but SAP GUI Scripting does not allow the requested interaction (HTTP 400).
        /// </summary>
        public const string ElementNotInteractable = "element not interactable";

        /// <summary>
        /// Specifies that the request arguments are invalid (HTTP 400).
        /// </summary>
        public const string InvalidArgument = "invalid argument";

        /// <summary>
        /// Specifies that a locator selector is malformed or uses an unsupported form (HTTP 400).
        /// </summary>
        public const string InvalidSelector = "invalid selector";

        /// <summary>
        /// Specifies that the WebDriver session does not exist (HTTP 404).
        /// </summary>
        public const string InvalidSessionId = "invalid session id";

        /// <summary>
        /// Specifies that no element matched the locator (HTTP 404).
        /// </summary>
        public const string NoSuchElement = "no such element";

        /// <summary>
        /// Specifies that no window matched the requested handle or name (HTTP 404).
        /// </summary>
        public const string NoSuchWindow = "no such window";

        /// <summary>
        /// Specifies that the server failed to attach to a SAP GUI session (HTTP 500).
        /// </summary>
        public const string SessionNotCreated = "session not created";

        /// <summary>
        /// Specifies that a cached element no longer resolves to a SAP GUI Scripting object (HTTP 404).
        /// </summary>
        public const string StaleElementReference = "stale element reference";

        /// <summary>
        /// Specifies that an operation did not complete within its time budget (HTTP 500).
        /// </summary>
        public const string Timeout = "timeout";

        /// <summary>
        /// Specifies that an unexpected server or COM failure occurred (HTTP 500).
        /// </summary>
        public const string UnknownError = "unknown error";

        /// <summary>
        /// Specifies that the operation has no SAP GUI Scripting equivalent (HTTP 500).
        /// </summary>
        public const string UnsupportedOperation = "unsupported operation";
        #endregion
    }
}
