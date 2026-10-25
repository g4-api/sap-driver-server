/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver1/#locator-strategies
 */
using System.ComponentModel.DataAnnotations;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the W3C WebDriver locator used by the find element and find elements endpoints.
    /// </summary>
    /// <remarks>
    /// The SAP driver evaluates every strategy against SAP GUI Scripting object identifiers and properties.
    /// It never falls back to screen coordinates, OCR, or UI Automation.
    /// </remarks>
    public class LocationStrategyModel
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the CSS selector strategy.
        /// </summary>
        /// <remarks>
        /// Only the simple forms <c>#sapId</c>, <c>Type</c>, <c>[attribute='value']</c>, and
        /// <c>Type[attribute='value']</c> are accepted, which covers the selectors Selenium clients
        /// generate for <c>By.Id</c>, <c>By.Name</c>, and <c>By.TagName</c>.
        /// </remarks>
        public const string CssSelector = "css selector";

        /// <summary>
        /// Specifies the SAP-specific strategy that resolves a SAP GUI Scripting identifier through <c>findById</c>.
        /// </summary>
        /// <remarks>
        /// The value is either an absolute identifier, such as <c>/app/con[0]/ses[0]/wnd[0]/usr/txtRSYST-BNAME</c>,
        /// or an identifier relative to the session or parent element, such as <c>wnd[0]/tbar[0]/btn[3]</c>.
        /// </remarks>
        public const string SapId = "id";

        /// <summary>
        /// Specifies the tag name strategy, which matches the SAP GUI Scripting <c>Type</c>, such as <c>GuiButton</c>.
        /// </summary>
        public const string TagName = "tag name";

        /// <summary>
        /// Specifies the XPath strategy, evaluated as XPath 1.0 against the SAP object model document.
        /// </summary>
        public const string Xpath = "xpath";
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the strategy used for locating elements.
        /// </summary>
        [Required]
        public string Using { get; set; }

        /// <summary>
        /// Gets or sets the selector evaluated by the selected strategy.
        /// </summary>
        [Required]
        public string Value { get; set; }
        #endregion
    }
}
