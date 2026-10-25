/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#contexts
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
        description: "W3C context endpoints: window state.",
        externalDocsUrl: "https://www.w3.org/TR/webdriver/#contexts")]
    public class ContextController(ISapDomain domain) : ControllerBase
    {
        // Provides the repositories; resolved per request.
        private readonly ISapDomain _domain = domain;

        [HttpPost]
        [Route("session/{session}/window/maximize")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Maximizes the current window.",
            description: "Calls maximize on the session's current SAP window and returns its rectangle.",
            Tags = ["Context"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Window maximized.",
            type: typeof(RectangleModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The session or window does not exist.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult InvokeWindowMaximize(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session)
        {
            // Maximize the window and return its new bounds.
            var (statusCode, rectangle) = _domain.SessionsRepository.SetWindowMaximized(session);

            return new JsonResult(new WebDriverResponseModel(session, rectangle))
            {
                StatusCode = statusCode
            };
        }
    }
}
