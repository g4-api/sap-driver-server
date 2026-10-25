/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#actions
 */
using System.Net.Mime;

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
        description: "W3C actions endpoint, executed as SAP virtual keys.",
        externalDocsUrl: "https://www.w3.org/TR/webdriver/#actions")]
    public class ActionsController(ISapDomain domain) : ControllerBase
    {
        // Provides the repositories; resolved per request.
        private readonly ISapDomain _domain = domain;

        [HttpPost]
        [Route("session/{session}/actions")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Performs key actions as SAP virtual keys.",
            description: "Supports Enter, Escape, F1-F12 with Shift/Ctrl, and paging keys. Character keys, Alt, Tab, arrows, " +
                "and pointer or wheel actions return 'unsupported operation'; nothing is sent when any action is unsupported.",
            Tags = ["Actions"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Actions performed.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "An action has no SAP virtual key equivalent (unsupported operation).",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult SendActions(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromBody]
            [SwaggerRequestBody(description: "The W3C actions payload.", Required = true)]
            ActionsModel data)
        {
            // Resolve the session the middleware already validated, then send the converted keys.
            var sessionModel = _domain.SessionsRepository.GetSession(session).Session;
            _domain.ActionsRepository.SendActions(sessionModel, data);

            return new JsonResult(new WebDriverResponseModel())
            {
                StatusCode = StatusCodes.Status200OK
            };
        }
    }
}
