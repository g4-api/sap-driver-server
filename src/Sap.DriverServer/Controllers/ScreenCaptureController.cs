/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#screen-capture
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
        description: "W3C screen capture endpoints.",
        externalDocsUrl: "https://www.w3.org/TR/webdriver/#screen-capture")]
    public class ScreenCaptureController(ISapDomain domain) : ControllerBase
    {
        // Provides the repositories; resolved per request.
        private readonly ISapDomain _domain = domain;

        [HttpGet]
        [Route("session/{session}/screenshot")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Captures the current window.",
            description: "Captures the session's current SAP window through GuiFrameWindow.HardCopy and returns a base64 PNG. " +
                "Unlike the UIA server, the image covers the SAP window, not the whole desktop.",
            Tags = ["ScreenCapture"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Screenshot captured.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The session or window does not exist.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetScreenshot(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session)
        {
            // Capture the window and return the base64 image in the value member.
            var (statusCode, screenshot) = _domain.SessionsRepository.NewScreenshot(session);

            return new JsonResult(new WebDriverResponseModel { Session = session, Value = screenshot })
            {
                StatusCode = statusCode
            };
        }
    }
}
