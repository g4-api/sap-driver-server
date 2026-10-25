using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Represents a classified SAP driver failure that the error middleware converts to a W3C error response.
    /// </summary>
    /// <remarks>
    /// The <see cref="Exception.Message"/> is client-facing and never contains raw COM text, field values, or
    /// credentials. The original exception, when any, is kept in <see cref="Exception.InnerException"/> for the
    /// server log only.
    /// </remarks>
    public class SapDriverException : Exception
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="SapDriverException"/> class without data or inner exception.
        /// </summary>
        /// <param name="error">The W3C error code from <see cref="WebDriverErrorCodes"/>.</param>
        /// <param name="errorKind">The SAP error kind from <see cref="SapErrorKinds"/>.</param>
        /// <param name="statusCode">The HTTP status code of the response.</param>
        /// <param name="message">The client-facing message.</param>
        public SapDriverException(string error, string errorKind, int statusCode, string message)
            : this(error, errorKind, statusCode, message, data: null, innerException: null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SapDriverException"/> class with structured data and an inner exception.
        /// </summary>
        /// <param name="error">The W3C error code from <see cref="WebDriverErrorCodes"/>.</param>
        /// <param name="errorKind">The SAP error kind from <see cref="SapErrorKinds"/>.</param>
        /// <param name="statusCode">The HTTP status code of the response.</param>
        /// <param name="message">The client-facing message.</param>
        /// <param name="data">Additional client-safe data, such as candidate session identifiers, or null.</param>
        /// <param name="innerException">The original failure kept for logging, or null.</param>
        public SapDriverException(
            string error,
            string errorKind,
            int statusCode,
            string message,
            IDictionary<string, object> data,
            Exception innerException)
            : base(message, innerException)
        {
            // Capture the classification once so the middleware renders it without re-inspecting the cause.
            Error = error;
            ErrorKind = errorKind;
            StatusCode = statusCode;
            ErrorData = data ?? new Dictionary<string, object>();
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the W3C error code.
        /// </summary>
        public string Error { get; }

        /// <summary>
        /// Gets additional client-safe data merged into the error response's <c>data</c> member.
        /// </summary>
        public IDictionary<string, object> ErrorData { get; }

        /// <summary>
        /// Gets the SAP error kind.
        /// </summary>
        public string ErrorKind { get; }

        /// <summary>
        /// Gets the HTTP status code of the response.
        /// </summary>
        public int StatusCode { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Creates a failure reporting that the scripting object model does not expose a member or interaction.
        /// </summary>
        /// <param name="message">The client-facing explanation.</param>
        /// <returns>An "unsupported operation" exception tagged <see cref="SapErrorKinds.NotExposedByScripting"/>.</returns>
        public static SapDriverException NewNotExposed(string message)
        {
            // Unsupported operations use HTTP 500, as defined by the W3C error table.
            return new SapDriverException(
                error: WebDriverErrorCodes.UnsupportedOperation,
                errorKind: SapErrorKinds.NotExposedByScripting,
                statusCode: StatusCodes.Status500InternalServerError,
                message);
        }

        /// <summary>
        /// Creates a failure reporting that the server failed to attach to a SAP GUI session.
        /// </summary>
        /// <param name="errorKind">The SAP error kind explaining the cause.</param>
        /// <param name="message">The client-facing explanation.</param>
        /// <returns>A "session not created" exception without data or inner exception.</returns>
        public static SapDriverException NewSessionNotCreated(string errorKind, string message)
        {
            // Route through the data overload with no additional data.
            return NewSessionNotCreated(errorKind, message, data: null);
        }

        /// <summary>
        /// Creates a failure reporting that the server failed to attach to a SAP GUI session, with client-safe data.
        /// </summary>
        /// <param name="errorKind">The SAP error kind explaining the cause.</param>
        /// <param name="message">The client-facing explanation.</param>
        /// <param name="data">Additional client-safe data, such as candidate sessions, or null.</param>
        /// <returns>A "session not created" exception.</returns>
        public static SapDriverException NewSessionNotCreated(string errorKind, string message, IDictionary<string, object> data)
        {
            // Session-creation failures use HTTP 500, as defined by the W3C error table.
            return new SapDriverException(
                error: WebDriverErrorCodes.SessionNotCreated,
                errorKind,
                statusCode: StatusCodes.Status500InternalServerError,
                message,
                data,
                innerException: null);
        }

        /// <summary>
        /// Creates a failure reporting that the server failed to attach to a SAP GUI session, keeping its cause.
        /// </summary>
        /// <param name="errorKind">The SAP error kind explaining the cause.</param>
        /// <param name="message">The client-facing explanation.</param>
        /// <param name="innerException">The original failure, kept for the server log only.</param>
        /// <returns>A "session not created" exception.</returns>
        public static SapDriverException NewSessionNotCreated(string errorKind, string message, Exception innerException)
        {
            // Session-creation failures use HTTP 500, as defined by the W3C error table.
            return new SapDriverException(
                error: WebDriverErrorCodes.SessionNotCreated,
                errorKind,
                statusCode: StatusCodes.Status500InternalServerError,
                message,
                data: null,
                innerException);
        }
        #endregion
    }
}
