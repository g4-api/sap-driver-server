/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#elements
 */
using System.Linq;
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
        description: "W3C element endpoints: locate SAP GUI Scripting controls and interact with them.",
        externalDocsUrl: "https://www.w3.org/TR/webdriver/#elements")]
    public class ElementsController(ISapDomain domain) : ControllerBase
    {
        // Provides the repositories; resolved per request.
        private readonly ISapDomain _domain = domain;

        [HttpPost]
        [Route("session/{session}/element/{element}/clear")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Clears a text control.",
            description: "Sets the text of a text field, password field, OK code field, or text edit to an empty string.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Control cleared.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
            description: "The control has no writable text (element not interactable).",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The element reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult ClearValue(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The element reference identifier.", Required = true)]
            string element)
        {
            // Clear the control and map a vanished control to a stale reference.
            var statusCode = _domain.ElementsRepository.ClearValue(session, element);

            return NewEmptyOrStaleResult(session, statusCode);
        }

        [HttpPost]
        [Route("session/{session}/element")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Finds an element.",
            description: "Locates the first control that matches the locator (xpath, id, tag name, or simple css selector).",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Element found.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
            description: "The locator strategy or selector is invalid.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "No control matches the locator.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult FindElement(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromBody]
            [SwaggerRequestBody(description: "The W3C locator.", Required = true)]
            LocationStrategyModel locator)
        {
            // Locate the control in the whole session.
            var (_, elementModel) = _domain.ElementsRepository.FindElement(session, locator);

            return NewElementResult(session, locator, elementModel);
        }

        [HttpPost]
        [Route("session/{session}/element/{element}/element")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Finds an element within an element.",
            description: "Locates the first descendant of the parent element that matches the locator.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Element found.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "No descendant matches, or the parent reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult FindElement(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The parent element reference identifier.", Required = true)]
            string element,
            [FromBody]
            [SwaggerRequestBody(description: "The W3C locator.", Required = true)]
            LocationStrategyModel locator)
        {
            // Locate the control within the parent's subtree.
            var (_, elementModel) = _domain.ElementsRepository.FindElement(session, element, locator);

            return NewElementResult(session, locator, elementModel);
        }

        [HttpPost]
        [Route("session/{session}/elements")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Finds elements.",
            description: "Locates every control that matches the locator, in document order.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Elements found; the list is empty when nothing matches.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult FindElements(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromBody]
            [SwaggerRequestBody(description: "The W3C locator.", Required = true)]
            LocationStrategyModel locator)
        {
            // Locate every match in the whole session.
            var ids = _domain.ElementsRepository.FindElements(session, locator).Select(i => i.Id);

            return new JsonResult(WebDriverResponseModel.NewElementsResponse(session, ids))
            {
                StatusCode = StatusCodes.Status200OK
            };
        }

        [HttpPost]
        [Route("session/{session}/element/{element}/elements")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Finds elements within an element.",
            description: "Locates every descendant of the parent element that matches the locator, in document order.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Elements found; the list is empty when nothing matches.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult FindElements(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The parent element reference identifier.", Required = true)]
            string element,
            [FromBody]
            [SwaggerRequestBody(description: "The W3C locator.", Required = true)]
            LocationStrategyModel locator)
        {
            // Locate every match within the parent's subtree.
            var ids = _domain.ElementsRepository.FindElements(session, element, locator).Select(i => i.Id);

            return new JsonResult(WebDriverResponseModel.NewElementsResponse(session, ids))
            {
                StatusCode = StatusCodes.Status200OK
            };
        }

        [HttpGet]
        [Route("session/{session}/element/{element}/attribute/{name}")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets an element attribute.",
            description: "Reads a whitelisted SAP GUI Scripting property; unknown or unexposed names return an empty string.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Attribute returned.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The element reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetElementAttribute(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The element reference identifier.", Required = true)]
            string element,
            [FromRoute]
            [SwaggerParameter(description: "The attribute name, such as text, tooltip, changeable, or iconName.", Required = true)]
            string name)
        {
            // Read the attribute and map a vanished control to a stale reference.
            var (statusCode, value) = _domain.ElementsRepository.GetElementAttribute(session, element, name);

            return NewValueOrStaleResult(session, statusCode, value);
        }

        [HttpGet]
        [Route("session/{session}/element/{element}/rect")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets the element rectangle.",
            description: "Returns the control's screen position and size in pixels as the W3C {x, y, width, height} object, " +
                "read from ScreenLeft, ScreenTop, Width, and Height.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Rectangle returned.",
            type: typeof(ElementRectModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The element reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetElementRect(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The element reference identifier.", Required = true)]
            string element)
        {
            // Read the geometry and map a vanished control to a stale reference.
            var (statusCode, rect) = _domain.ElementsRepository.GetElementRect(session, element);
            if (statusCode == StatusCodes.Status404NotFound)
            {
                return new NotFoundObjectResult(WebDriverResponseModel.NewStaleReferenceResponse(session));
            }

            return new JsonResult(new WebDriverResponseModel { Session = session, Value = rect })
            {
                StatusCode = statusCode
            };
        }

        [HttpGet]
        [Route("session/{session}/element/{element}/text")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Gets the element text.",
            description: "Reads the Text property; password fields always return an empty string.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Text returned.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The element reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult GetElementText(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The element reference identifier.", Required = true)]
            string element)
        {
            // Read the text and map a vanished control to a stale reference.
            var (statusCode, text) = _domain.ElementsRepository.GetElementText(session, element);

            return NewValueOrStaleResult(session, statusCode, text);
        }

        [HttpPost]
        [Route("session/{session}/element/{element}/click")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Clicks an element.",
            description: "Performs the control's click equivalent: press (buttons), select (tabs, radio buttons, menus), " +
                "toggle (check boxes), or setFocus (fields, labels). Shells return 'unsupported operation'.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Click performed.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The element reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status500InternalServerError,
            description: "The control type has no click equivalent in SAP GUI Scripting (unsupported operation).",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult InvokeClick(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The element reference identifier.", Required = true)]
            string element)
        {
            // Click and map a vanished control to a stale reference.
            var statusCode = _domain.ElementsRepository.InvokeClick(session, element);

            return NewEmptyOrStaleResult(session, statusCode);
        }

        [HttpPost]
        [Route("session/{session}/element/{element}/value")]
        #region *** Open API Documentation ***
        [SwaggerOperation(
            summary: "Sets the element value.",
            description: "Replaces the text of a text control, or selects a combo box entry by key.",
            Tags = ["Elements"])]
        [SwaggerResponse(StatusCodes.Status200OK,
            description: "Value set.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status400BadRequest,
            description: "The control has no writable text (element not interactable).",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        [SwaggerResponse(StatusCodes.Status404NotFound,
            description: "The element reference is stale.",
            type: typeof(WebDriverResponseModel),
            contentTypes: MediaTypeNames.Application.Json)]
        #endregion
        public IActionResult SetValue(
            [FromRoute]
            [SwaggerParameter(description: "The WebDriver session identifier.", Required = true)]
            string session,
            [FromRoute]
            [SwaggerParameter(description: "The element reference identifier.", Required = true)]
            string element,
            [FromBody]
            [SwaggerRequestBody(description: "The text to write.", Required = true)]
            TextInputModel textData)
        {
            // Write the text and map a vanished control to a stale reference.
            var statusCode = _domain.ElementsRepository.SetValue(session, element, textData.Text);

            return NewEmptyOrStaleResult(session, statusCode);
        }

        // Builds the element reference envelope, or the no such element error when nothing matched.
        private static IActionResult NewElementResult(string session, LocationStrategyModel locator, SapElementModel elementModel)
        {
            // Report a miss with the locator echoed in the message.
            if (elementModel is null)
            {
                return new NotFoundObjectResult(WebDriverResponseModel.NewElementNotFoundResponse(locator, session));
            }

            return new JsonResult(WebDriverResponseModel.NewElementResponse(session, elementModel.Id))
            {
                StatusCode = StatusCodes.Status200OK
            };
        }

        // Builds the empty success envelope, or the stale reference error when the control no longer resolves.
        private static IActionResult NewEmptyOrStaleResult(string session, int statusCode)
        {
            // Report vanished controls with the UIA stale reference error.
            if (statusCode == StatusCodes.Status404NotFound)
            {
                return new NotFoundObjectResult(WebDriverResponseModel.NewStaleReferenceResponse(session));
            }

            return new JsonResult(new WebDriverResponseModel())
            {
                StatusCode = statusCode
            };
        }

        // Builds the value envelope, or the stale reference error when the control no longer resolves.
        private static IActionResult NewValueOrStaleResult(string session, int statusCode, string value)
        {
            // Report vanished controls with the UIA stale reference error.
            if (statusCode == StatusCodes.Status404NotFound)
            {
                return new NotFoundObjectResult(WebDriverResponseModel.NewStaleReferenceResponse(session));
            }

            return new JsonResult(new WebDriverResponseModel { Session = session, Value = value })
            {
                StatusCode = statusCode
            };
        }
    }
}
