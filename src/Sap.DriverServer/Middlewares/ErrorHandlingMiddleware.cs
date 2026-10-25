using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Middlewares
{
    /// <summary>
    /// Validates session route values and converts failures into W3C WebDriver error responses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The middleware runs after routing, so it rejects an unknown <c>{session}</c> with "invalid session id" before
    /// any controller or SAP call runs (DELETE requests are exempt, as in the UIA server).
    /// </para>
    /// <para>
    /// <see cref="SapDriverException"/> failures, and any failure that contains a COM exception, become W3C errors
    /// with the SAP error kind in <c>data.sapErrorKind</c> and no stack trace, so COM details and SAP texts stay in the
    /// server log. Other failures keep the UIA server's "Internal Server Error" shape.
    /// </para>
    /// </remarks>
    /// <param name="logger">The logger for classified and unexpected failures.</param>
    /// <param name="jsonOptions">The MVC JSON options, so errors serialize like controller results.</param>
    /// <param name="next">The next middleware in the pipeline.</param>
    public class ErrorHandlingMiddleware(
        ILogger<ErrorHandlingMiddleware> logger,
        JsonSerializerOptions jsonOptions,
        RequestDelegate next)
    {
        #region *** Fields       ***
        // Serializes error responses with the controllers' naming and null-handling conventions.
        private readonly JsonSerializerOptions _jsonOptions = jsonOptions;

        // Records failures; SAP failures are logged by kind and HRESULT, never by SAP text.
        private readonly ILogger<ErrorHandlingMiddleware> _logger = logger;

        // Continues the pipeline once the session route value is valid.
        private readonly RequestDelegate _next = next;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Builds the UIA-compatible "Bad Request" response used for model validation failures.
        /// </summary>
        /// <param name="context">The action context whose model state is invalid.</param>
        /// <returns>A 400 result whose <c>value</c> carries the validation problem details in <c>data</c>.</returns>
        public static IActionResult NewBadRequestResult(ActionContext context)
        {
            // Wrap the standard problem details in the UIA server's envelope.
            var response = new
            {
                Value = new
                {
                    Error = "Bad Request",
                    Message = "The request could not be understood by the server due to malformed syntax.",
                    Data = new ValidationProblemDetails(context.ModelState)
                }
            };

            return new BadRequestObjectResult(response);
        }

        /// <summary>
        /// Processes one request: validates its session, runs the pipeline, and renders failures.
        /// </summary>
        /// <param name="context">The HTTP context.</param>
        /// <param name="domain">The request-scoped domain used to validate the session route value.</param>
        /// <returns>A task that completes when the response is written.</returns>
        public async Task InvokeAsync(HttpContext context, ISapDomain domain)
        {
            try
            {
                // Reject requests for unknown sessions before any controller or SAP call runs.
                var session = context.Request.RouteValues["session"]?.ToString();
                var isSessionChecked = session is not null && !HttpMethods.IsDelete(context.Request.Method);

                if (isSessionChecked && domain.SessionsRepository.GetSession(session).StatusCode == StatusCodes.Status404NotFound)
                {
                    _logger.LogError("Invalid session ID: {Session}", session);
                    var invalidSession = WebDriverResponseModel.NewInvalidSessionResponse(session);
                    await WriteAsync(context, StatusCodes.Status404NotFound, json: JsonSerializer.Serialize(invalidSession, _jsonOptions)).ConfigureAwait(false);

                    return;
                }

                // Run the controller pipeline.
                await _next(context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Render the failure unless the response is already streaming, in which case only logging is possible.
                if (context.Response.HasStarted)
                {
                    _logger.LogError(exception, "A failure occurred after the response started.");
                    throw;
                }

                await WriteFailureAsync(context, exception).ConfigureAwait(false);
            }
        }

        // Converts a SAP or COM failure into its W3C error, or an unexpected failure into the UIA fault shape.
        private async Task WriteFailureAsync(HttpContext context, Exception exception)
        {
            var session = context.Request.RouteValues["session"]?.ToString();
            var driverException = SapErrorMapper.ConvertToDriverException(exception);

            // Render classified SAP failures without stack traces or COM text.
            if (driverException is not null)
            {
                _logger.LogWarning(
                    "Request {Method} {Path} failed: {Error} ({ErrorKind}).",
                    context.Request.Method,
                    context.Request.Path,
                    driverException.Error,
                    driverException.ErrorKind);

                var data = new Dictionary<string, object>(driverException.ErrorData);
                if (!string.IsNullOrEmpty(driverException.ErrorKind))
                {
                    data[SapErrorKinds.DataKey] = driverException.ErrorKind;
                }

                var response = WebDriverResponseModel.NewErrorResponse(session, error: new WebDriverErrorModel
                {
                    Data = data.Count == 0 ? null : data,
                    Error = driverException.Error,
                    Message = driverException.Message
                });

                await WriteAsync(context, driverException.StatusCode, json: JsonSerializer.Serialize(response, _jsonOptions)).ConfigureAwait(false);
                return;
            }

            // Render unexpected server faults in the UIA server's shape.
            _logger.LogError(exception, "An unhandled exception occurred.");
            var baseException = exception.GetBaseException();
            var fault = new
            {
                Value = new
                {
                    Error = "Internal Server Error",
                    baseException.Message,
                    Stacktrace = $"{baseException}"
                }
            };

            await WriteAsync(context, StatusCodes.Status500InternalServerError, json: JsonSerializer.Serialize(fault, _jsonOptions)).ConfigureAwait(false);
        }

        // Replaces any buffered output with a serialized JSON body and status code.
        private static Task WriteAsync(HttpContext context, int statusCode, string json)
        {
            // Clear headers and status set by the failed pipeline before writing the error.
            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            return context.Response.WriteAsync(json);
        }
        #endregion
    }
}
