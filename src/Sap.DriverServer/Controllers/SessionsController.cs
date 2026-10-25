/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#sessions
 */
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Runtime.InteropServices;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Controllers
{
    [Route("wd/hub"), Route("/")]
    [ApiController]
    [SwaggerTag(
        description: "W3C session endpoints: attach to and detach from SAP GUI sessions, windows, and status.",
        externalDocsUrl: "https://www.w3.org/TR/webdriver/#sessions")]
    public class SessionsController(ISapDomain domain) : ControllerBase
    {
        // Provides the repositories; resolved per request.
        private readonly ISapDomain _domain = domain;

        [HttpDelete]
        [Route("session/{session}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Detaches the specified session.",
            description: "Removes the WebDriver session. The SAP GUI session and its windows stay open.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Session detached.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The session does not exist.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult DeleteSession(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session)
        {
            // Detach the session and report the outcome with the UIA response envelope.
            var statusCode = _domain.SessionsRepository.DeleteSession(session);

            return new JsonResult(new WebDriverResponseModel())
            {
                StatusCode = statusCode
            };
        }

        [HttpDelete]
        [Route("session")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Detaches all sessions.",
            description: "Removes every WebDriver session. SAP GUI sessions and windows stay open.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "All sessions detached.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult DeleteSessions()
        {
            // Detach every session; the repository snapshot keeps enumeration safe while removing.
            var (_, sessions) = _domain.SessionsRepository.GetSessions();

            foreach (var session in sessions)
            {
                _domain.SessionsRepository.DeleteSession(session.SessionId);
            }

            return new JsonResult(new WebDriverResponseModel())
            {
                StatusCode = StatusCodes.Status200OK
            };
        }

        [HttpGet]
        [Route("session/{session}/dom")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets the SAP object model document of the session.",
            description: "Returns the XML document built from the SAP GUI Scripting object tree of every open window.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Document returned.",
            type: typeof(string),
            contentTypes: MediaTypeNames.Application.Xml)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The session does not exist.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetDocumentObjectModel(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session)
        {
            // Render the session document as XML content, as the UIA server does.
            var (statusCode, objectModel) = _domain.DocumentRepository.GetPageSource(session);

            return new ContentResult
            {
                Content = statusCode == StatusCodes.Status200OK ? objectModel : NewErrorDocument(session),
                ContentType = MediaTypeNames.Application.Xml,
                StatusCode = statusCode
            };
        }

        [HttpPost]
        [Route("session/{session}/element/dom")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets the SAP object model document of a located element.",
            description: "Finds the element with the locator and returns the XML document of its subtree.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Document returned.",
            type: typeof(string),
            contentTypes: MediaTypeNames.Application.Xml)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "No element matches the locator.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetDocumentObjectModel(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromBody]
            [SwaggerRequestBody(description: "The locator of the element whose subtree is returned.", Required = true)]
            LocationStrategyModel locator)
        {
            // Locate the element first; a miss is reported as "no such element".
            var (findStatusCode, elementModel) = _domain.ElementsRepository.FindElement(session, locator);
            if (findStatusCode == StatusCodes.Status404NotFound || elementModel is null)
            {
                return NotFound(WebDriverResponseModel.NewElementNotFoundResponse(locator, session));
            }

            // Render the element subtree as XML content.
            var (domStatusCode, objectModel) = _domain.DocumentRepository.GetElementSource(session, elementModel);

            return new ContentResult
            {
                Content = domStatusCode == StatusCodes.Status200OK ? objectModel : NewErrorDocument(session),
                ContentType = MediaTypeNames.Application.Xml,
                StatusCode = domStatusCode
            };
        }

        [HttpGet]
        [Route("status")]
        [Produces(MediaTypeNames.Application.Json)]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets the server status.",
            description: "Reports whether a new session can be created and whether SAP GUI Scripting is reachable.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Status returned.",
            type: typeof(SessionStatusModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetStatus()
        {
            // Use the UIA readiness rule: one active session fills the slot.
            var sessions = _domain.SessionsRepository.Sessions.Keys;
            var isFull = sessions.Count > 0;
            var message = isFull
                ? "Current sessions stack is full, the maximum allowed sessions number is 1"
                : "Sessions slots available, can create new session";

            // Report the OS details and the SAP GUI Scripting reachability.
            return new JsonResult(new
            {
                Value = new SessionStatusModel
                {
                    Message = message,
                    OperatingSystem = new Dictionary<string, object>
                    {
                        ["arch"] = $"{RuntimeInformation.OSArchitecture}",
                        ["name"] = RuntimeInformation.OSDescription,
                        ["version"] = $"{Environment.OSVersion.Version}"
                    },
                    Ready = !isFull,
                    Sap = _domain.SessionsRepository.GetScriptingStatus(),
                    Sessions = sessions
                }
            });
        }

        [HttpGet]
        [Route("session/{session}/window")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets the current window handle.",
            description: "Returns a JSON string with the hexadecimal handle and title of the session's current SAP window.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Window handle returned.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The session or window does not exist.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetWindowHandle(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session)
        {
            // Read the current window's handle and title.
            var (statusCode, handle) = _domain.SessionsRepository.GetHandle(session);

            return new JsonResult(new WebDriverResponseModel(session, value: handle))
            {
                StatusCode = statusCode
            };
        }

        [HttpGet]
        [Route("session/{session}/window/handles")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets all window handles.",
            description: "Returns the hexadecimal handles of every open window of the attached SAP GUI session.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Window handles returned.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetWindowHandles(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session)
        {
            // List the session's window handles.
            var (statusCode, value) = _domain.SessionsRepository.GetHandles(id: session);

            return new JsonResult(new WebDriverResponseModel(session, value))
            {
                StatusCode = statusCode
            };
        }

        [HttpPost]
        [Route("session")]
        [Produces(MediaTypeNames.Application.Json)]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Attaches a new session to an open SAP GUI session.",
            description: "Selects exactly one open SAP GUI session using the sap:options capability. " +
                "Fails with 'session not created' when no session or more than one session matches.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Session created.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
            description: "The capabilities or sap:options are invalid.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "SAP GUI or scripting is unavailable, or the selection matched zero or several sessions.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult NewSession(
            [FromBody]
            [SwaggerRequestBody(description: "The W3C capabilities, including sap:options.", Required = true)]
            NewSessionModel capabilities)
        {
            // Attach the session; selection failures are thrown and rendered by the error middleware.
            var (statusCode, session) = _domain.SessionsRepository.NewSession(capabilities);

            return new JsonResult(session)
            {
                StatusCode = statusCode
            };
        }

        [HttpPost]
        [Route("session/{session}/window")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Switches to a window.",
            description: "Focuses the SAP window identified by hexadecimal handle, window name (wnd[n]), or exact title.",
            Tags = ["Sessions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Switched to the window.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
            description: "The window handle or name is empty.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "No open window matches.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult SwitchWindow(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromBody]
            [SwaggerRequestBody(description: "The window handle, name, or title.", Required = true)]
            WindowHandleRequestModel requestModel)
        {
            // Switch and translate the status to the UIA error value shape.
            var statusCode = _domain.SessionsRepository.SwitchWindow(id: session, requestModel.Handle);
            var errorCode = statusCode switch
            {
                StatusCodes.Status404NotFound => WebDriverErrorCodes.NoSuchWindow,
                StatusCodes.Status400BadRequest => WebDriverErrorCodes.InvalidArgument,
                _ => null
            };
            var value = errorCode is null ? null : new Dictionary<string, string> { ["error"] = errorCode };

            return new JsonResult(new WebDriverResponseModel(session, value))
            {
                StatusCode = statusCode
            };
        }

        // Builds the XML error document returned by the DOM endpoints, matching the UIA server.
        private static string NewErrorDocument(string session)
        {
            // Use the UIA root element name so clients parsing the error document keep working.
            return $"<Desktop><Error>Session with ID {session} not found.</Error></Desktop>";
        }
    }
}
