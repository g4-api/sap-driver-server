using System.Collections.Generic;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the response envelope for WebDriver operations.
    /// </summary>
    /// <remarks>
    /// The envelope and its factory methods match the UIA driver server so G4 clients parse both servers identically.
    /// Error messages identify this server as <c>SapDriver</c> in the session information suffix.
    /// </remarks>
    public class WebDriverResponseModel
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="WebDriverResponseModel"/> class with an empty value object.
        /// </summary>
        public WebDriverResponseModel()
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebDriverResponseModel"/> class for a session with an empty value object.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        public WebDriverResponseModel(string session)
            : this(session, value: new Dictionary<string, object>())
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebDriverResponseModel"/> class with the specified session and value.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="value">The value of the response.</param>
        public WebDriverResponseModel(string session, object value)
        {
            // Capture the envelope members exactly as supplied; null values are omitted by the serializer settings.
            Session = session;
            Value = value;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        public string Session { get; set; }

        /// <summary>
        /// Gets or sets the value returned by the WebDriver operation, or a <see cref="WebDriverErrorModel"/> on failure.
        /// </summary>
        public object Value { get; set; } = new Dictionary<string, object>();
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Creates a response indicating that an element was not found.
        /// </summary>
        /// <param name="locationStrategy">The location strategy used to find the element.</param>
        /// <param name="session">The session identifier.</param>
        /// <returns>A <see cref="WebDriverResponseModel"/> carrying a <see cref="WebDriverErrorCodes.NoSuchElement"/> error.</returns>
        public static WebDriverResponseModel NewElementNotFoundResponse(LocationStrategyModel locationStrategy, string session)
        {
            // Echo the locator in the same JSON-like message format the UIA server uses.
            var message = "Unable to locate element: " +
                $"{{\"method\": \"{locationStrategy.Using}\", \"selector\":\"{locationStrategy.Value}\"}}";

            return NewErrorResponse(session, error: new WebDriverErrorModel
            {
                Error = WebDriverErrorCodes.NoSuchElement,
                Message = message
            });
        }

        /// <summary>
        /// Creates a response for one located element.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="id">The element reference identifier.</param>
        /// <returns>A <see cref="WebDriverResponseModel"/> carrying the W3C element reference object.</returns>
        public static WebDriverResponseModel NewElementResponse(string session, string id)
        {
            // Wrap the reference in the W3C web element identifier key.
            return new WebDriverResponseModel
            {
                Session = session,
                Value = new Dictionary<string, string>
                {
                    [ElementProperties.ElementReference] = id
                }
            };
        }

        /// <summary>
        /// Creates a response for multiple located elements.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="ids">The element reference identifiers.</param>
        /// <returns>A <see cref="WebDriverResponseModel"/> carrying a list of W3C element reference objects.</returns>
        public static WebDriverResponseModel NewElementsResponse(string session, IEnumerable<string> ids)
        {
            // Wrap every reference in its own W3C element object, preserving the locator's document order.
            var elements = new List<Dictionary<string, string>>();

            foreach (var id in ids)
            {
                elements.Add(new Dictionary<string, string>
                {
                    [ElementProperties.ElementReference] = id
                });
            }

            return new WebDriverResponseModel
            {
                Session = session,
                Value = elements
            };
        }

        /// <summary>
        /// Creates an error response that places a W3C error object in the value member.
        /// </summary>
        /// <param name="session">The session identifier, or null for session-less endpoints.</param>
        /// <param name="error">The error object carrying the W3C code, message, and optional data.</param>
        /// <returns>A <see cref="WebDriverResponseModel"/> carrying <paramref name="error"/>.</returns>
        public static WebDriverResponseModel NewErrorResponse(string session, WebDriverErrorModel error)
        {
            // Place the error object in the value member, which is where W3C clients look for it.
            return new WebDriverResponseModel
            {
                Session = session,
                Value = error
            };
        }

        /// <summary>
        /// Creates a response indicating an invalid session identifier.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <returns>A <see cref="WebDriverResponseModel"/> carrying a <see cref="WebDriverErrorCodes.InvalidSessionId"/> error.</returns>
        public static WebDriverResponseModel NewInvalidSessionResponse(string session)
        {
            // Keep the UIA wording so clients matching on the message keep working.
            var message = "The session ID provided is invalid. The session might have been closed or does not exist. " +
                $"(Session info: SapDriver={session})";

            return NewErrorResponse(session, error: new WebDriverErrorModel
            {
                Error = WebDriverErrorCodes.InvalidSessionId,
                Message = message
            });
        }

        /// <summary>
        /// Creates a response indicating a stale element reference.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <returns>A <see cref="WebDriverResponseModel"/> carrying a <see cref="WebDriverErrorCodes.StaleElementReference"/> error.</returns>
        public static WebDriverResponseModel NewStaleReferenceResponse(string session)
        {
            // Keep the UIA wording so clients matching on the message keep working.
            var message = "Stale element reference: element is not attached to the application document " +
                $"(Session info: SapDriver={session})";

            return NewErrorResponse(session, error: new WebDriverErrorModel
            {
                Error = WebDriverErrorCodes.StaleElementReference,
                Message = message
            });
        }

        /// <summary>
        /// Creates a response indicating an unsupported operation, using the generic UIA message.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <returns>A <see cref="WebDriverResponseModel"/> carrying a <see cref="WebDriverErrorCodes.UnsupportedOperation"/> error.</returns>
        public static WebDriverResponseModel NewUnsupportedOperationResponse(string session)
        {
            // Route through the complete overload with the generic reason used by the UIA server.
            return NewUnsupportedOperationResponse(session, reason: "The requested operation is not supported.");
        }

        /// <summary>
        /// Creates a response indicating an unsupported operation with a specific reason.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="reason">The client-facing explanation of why SAP GUI Scripting cannot perform the operation.</param>
        /// <returns>A <see cref="WebDriverResponseModel"/> carrying a <see cref="WebDriverErrorCodes.UnsupportedOperation"/> error.</returns>
        public static WebDriverResponseModel NewUnsupportedOperationResponse(string session, string reason)
        {
            // Tag the error with the SAP kind so clients can tell "not exposed by scripting" from other failures.
            var message = $"{reason} (Session info: SapDriver={session})";
            var data = new Dictionary<string, object>
            {
                [SapErrorKinds.DataKey] = SapErrorKinds.NotExposedByScripting
            };

            return NewErrorResponse(session, error: new WebDriverErrorModel
            {
                Data = data,
                Error = WebDriverErrorCodes.UnsupportedOperation,
                Message = message
            });
        }
        #endregion
    }
}
