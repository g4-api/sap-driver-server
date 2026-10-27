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
    [SwaggerTag(description: "SAP-specific endpoints: session discovery, virtual keys, the status bar, and " +
        "allow-listed control methods and properties.")]
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

        [HttpPost]
        [Route("session/{session}/sap/element/{element}/call")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Calls an allow-listed method on a control.",
            description: "Calls a SAP GUI Scripting method that the W3C element commands do not cover, such as " +
                "pressToolbarButton on a grid view shell or expandNode on a tree shell. Only methods on the driver's " +
                "allow-list for the control family are accepted. Scalars are returned as they are, collections as " +
                "arrays, and controls as element references; methods without a result return an empty object.",
            Tags = ["Sap"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Method called; the value holds its result.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
            description: "The request body or an argument is invalid.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The element reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "The method is not on the allow-list, or SAP GUI refused the call.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult InvokeElementMethod(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The element reference identifier.", Required = true)]
            string element,
            [FromBody]
            [SwaggerRequestBody(description: "The method name and positional scalar arguments.", Required = true)]
            SapMethodCallModel call)
        {
            // Call the method on the control; refused methods and SAP failures are rendered by the error middleware.
            var (statusCode, value) = _domain.ElementsRepository.InvokeElementMethod(session, element, call);
            if (statusCode == StatusCodes.Status404NotFound)
            {
                return new NotFoundObjectResult(WebDriverResponseModel.NewStaleReferenceResponse(session));
            }

            // Return the result; a method without one returns the empty object other commands return.
            return new JsonResult(new WebDriverResponseModel(session, value ?? new Dictionary<string, object>()))
            {
                StatusCode = statusCode
            };
        }

        [HttpPost]
        [Route("session/{session}/sap/element/{element}/property")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Sets an allow-listed property on a control.",
            description: "Sets a SAP GUI Scripting property that the W3C element commands do not cover, such as " +
                "selectedRows on a grid view shell or verticalScrollbar.position on a table control, scroll container, " +
                "or user area. Only properties on the driver's allow-list for the control family are accepted.",
            Tags = ["Sap"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Property set.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
            description: "The request body or the value is invalid.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The element reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "The property is not on the allow-list, or SAP GUI refused the value.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult SetElementProperty(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The element reference identifier.", Required = true)]
            string element,
            [FromBody]
            [SwaggerRequestBody(description: "The property name and scalar value.", Required = true)]
            SapPropertyModel property)
        {
            // Set the property on the control; refused names and SAP failures are rendered by the error middleware.
            var statusCode = _domain.ElementsRepository.SetElementProperty(session, element, property);
            if (statusCode == StatusCodes.Status404NotFound)
            {
                return new NotFoundObjectResult(WebDriverResponseModel.NewStaleReferenceResponse(session));
            }

            return new JsonResult(new WebDriverResponseModel(session))
            {
                StatusCode = statusCode
            };
        }
    }
}
