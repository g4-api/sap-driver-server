using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Swashbuckle.AspNetCore.Annotations;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Controllers
{
    [Route("wd/hub"), Route("/")]
    [ApiController]
    [SwaggerTag(description: "UIA server User32 endpoints. They inject raw Windows input and have no SAP GUI Scripting equivalent.")]
    public class User32Controller : ControllerBase
    {
        // Explains why every User32 route is refused instead of emulated.
        private const string Reason = "User32 raw input is not available in the SAP driver. SAP GUI Scripting drives controls " +
            "through their scripting members; use the element, actions, and sap/vkey endpoints instead.";

        [HttpGet]
        [Route("session/{session}/user32/{**path}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Refuses User32 read endpoints (not supported).",
            description: "Every GET under user32/ returns 'unsupported operation'.",
            Tags = ["User32"])]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "Unsupported operation.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The path segment is part of the catch-all route contract.")]
        public IActionResult GetUser32(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The UIA User32 sub-path, such as layouts.", Required = true)]
            string path)
        {
            // Refuse explicitly; raw input bypasses SAP GUI Scripting.
            return NewUnsupportedResult(session);
        }

        [HttpPost]
        [Route("session/{session}/user32/{**path}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Refuses User32 input endpoints (not supported).",
            description: "Every POST under user32/ (click, dclick, inputs, value, modified, mouse/move, copy, paste, " +
                "layouts, and element variants) returns 'unsupported operation'.",
            Tags = ["User32"])]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "Unsupported operation.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The path segment is part of the catch-all route contract.")]
        public IActionResult InvokeUser32(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The UIA User32 sub-path, such as click or inputs.", Required = true)]
            string path)
        {
            // Refuse explicitly; raw input bypasses SAP GUI Scripting.
            return NewUnsupportedResult(session);
        }

        // Builds the shared "unsupported operation" response for every User32 route.
        private static JsonResult NewUnsupportedResult(string session)
        {
            // Use HTTP 500, the W3C status for unsupported operations.
            return new JsonResult(WebDriverResponseModel.NewUnsupportedOperationResponse(session, Reason))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
