/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#document
 */
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
    [SwaggerTag(
        description: "W3C document endpoints: page source and script execution.",
        externalDocsUrl: "https://www.w3.org/TR/webdriver/#document")]
    public class DocumentController(ISapDomain domain) : ControllerBase
    {
        // Provides the repositories; resolved per request.
        private readonly ISapDomain _domain = domain;

        [HttpGet]
        [Route("session/{session}/source")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets the page source.",
            description: "Returns the SAP object model XML document of the session in the value member.",
            Tags = ["Document"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Source returned.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The session does not exist.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetSource(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session)
        {
            // Render the session document; an unknown session yields the UIA XML error document.
            var (statusCode, objectModel) = _domain.DocumentRepository.GetPageSource(session);
            var content = statusCode == StatusCodes.Status200OK
                ? objectModel
                : $"<Desktop><Error>Session with ID {session} not found.</Error></Desktop>";

            return new JsonResult(new WebDriverResponseModel { Value = content })
            {
                StatusCode = statusCode
            };
        }

        [HttpPost]
        [Route("session/{session}/execute/sync")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Executes a script (not supported).",
            description: "The UIA server runs PowerShell here. SAP GUI Scripting offers no script host to the driver, " +
                "so this endpoint always returns 'unsupported operation'.",
            Tags = ["Document"])]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "Unsupported operation.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult InvokeScript(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromBody]
            [SwaggerRequestBody(description: "The script payload; it is not executed.", Required = true)]
            ScriptInputModel scriptData)
        {
            // Refuse explicitly; executing client scripts bypasses the SAP scripting boundary.
            const string reason = "Script execution is not available in the SAP driver; use the element and SAP endpoints instead.";

            return new JsonResult(WebDriverResponseModel.NewUnsupportedOperationResponse(session, reason))
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
