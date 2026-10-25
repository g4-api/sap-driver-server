using System.Collections.Generic;
using System.Net.Mime;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Controllers
{
    [Produces(MediaTypeNames.Application.Json)]
    [Route("wd/hub"), Route("/")]
    [ApiController]
    [SwaggerTag(description: "SAP-specific endpoints: session discovery, virtual keys, and the status bar.")]
    public class SapController(ISapDomain domain) : ControllerBase
    {
        // Provides the repositories; resolved per request.
        private readonly ISapDomain _domain = domain;

        [HttpGet]
        [Route("sap/sessions")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Lists the scriptable SAP GUI connections and sessions.",
            description: "Returns identifiers, SAP Logon descriptions, system IDs, and clients, so a client can choose " +
                "sap:options values. User names, transactions, and screen contents are never returned.",
            Tags = ["Sap"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Connections returned.",
            type: typeof(IEnumerable<SapConnectionInfoModel>),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "SAP GUI or scripting is unavailable.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetConnections()
        {
            // Discover the connections; prerequisite failures are rendered by the error middleware.
            var (statusCode, connections) = _domain.SessionsRepository.GetConnections();

            return new JsonResult(new WebDriverResponseModel { Value = connections })
            {
                StatusCode = statusCode
            };
        }

        [HttpPost]
        [Route("session/{session}/sap/element/at")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Finds the element at a screen point.",
            description: "Hit-tests the attached session with findByPosition and returns an element reference, the SAP id, " +
                "type, rectangle, and the inner-object description (such as a grid cell). Inspectors use it for hover.",
            Tags = ["Sap"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Element found.",
            type: typeof(SapElementAtModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The point is not over a control of the session (no such element).",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult FindElementAt(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromBody]
            [SwaggerRequestBody(description: "The screen point in pixels.", Required = true)]
            SapPointModel point)
        {
            // Hit-test the attached session; a miss is reported as "no such element".
            var (statusCode, elementAt) = _domain.ElementsRepository.FindElementAt(session, point);
            if (elementAt is null)
            {
                var error = new WebDriverErrorModel
                {
                    Error = WebDriverErrorCodes.NoSuchElement,
                    Message = $"No SAP GUI control at screen point ({point.X}, {point.Y})."
                };

                return new NotFoundObjectResult(WebDriverResponseModel.NewErrorResponse(session, error));
            }

            return new JsonResult(new WebDriverResponseModel(session, elementAt))
            {
                StatusCode = statusCode
            };
        }

        [HttpGet]
        [Route("session/{session}/sap/statusbar")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Reads the status bar.",
            description: "Returns the message type, id, number, and text of the main window's status bar.",
            Tags = ["Sap"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Status bar returned.",
            type: typeof(SapStatusBarModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetStatusBar(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session)
        {
            // Read the status bar of the attached session.
            var (statusCode, statusBar) = _domain.SessionsRepository.GetStatusBar(session);

            return new JsonResult(new WebDriverResponseModel(session, statusBar))
            {
                StatusCode = statusCode
            };
        }

        [HttpPost]
        [Route("session/{session}/sap/vkey")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Sends a SAP virtual key.",
            description: "Calls sendVKey on the requested window (or the current window), for example 0 for Enter or 3 for Back.",
            Tags = ["Sap"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Virtual key sent.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The window is not open.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult SendVirtualKey(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromBody]
            [SwaggerRequestBody(description: "The virtual key and optional window name.", Required = true)]
            SapVirtualKeyModel virtualKey)
        {
            // Send the key to the attached session.
            var statusCode = _domain.SessionsRepository.SendVirtualKey(session, virtualKey);

            return new JsonResult(new WebDriverResponseModel(session))
            {
                StatusCode = statusCode
            };
        }
    }
}
