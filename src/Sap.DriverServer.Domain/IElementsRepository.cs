using System.Collections.Generic;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Locates SAP GUI Scripting controls and performs element-level interactions.
    /// </summary>
    /// <remarks>
    /// Element references map to SAP scripting identifiers and are re-resolved through <c>findById</c> on every call.
    /// A 404 status means the WebDriver session or the element reference is unknown, or the control no longer exists.
    /// </remarks>
    public interface IElementsRepository
    {
        #region *** Methods      ***
        /// <summary>
        /// Clears the text of a control.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The element reference identifier.</param>
        /// <returns>200 when cleared; 404 when the element is stale.</returns>
        int ClearValue(string session, string element);

        /// <summary>
        /// Finds the first control in the attached SAP session that matches a locator.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="locationStrategy">The W3C locator.</param>
        /// <returns>200 with the element, or 404 with null when nothing matches.</returns>
        (int Status, SapElementModel ElementModel) FindElement(string session, LocationStrategyModel locationStrategy);

        /// <summary>
        /// Finds the first descendant of an element that matches a locator.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The parent element reference identifier.</param>
        /// <param name="locationStrategy">The W3C locator.</param>
        /// <returns>200 with the element, or 404 with null when nothing matches.</returns>
        (int Status, SapElementModel ElementModel) FindElement(string session, string element, LocationStrategyModel locationStrategy);

        /// <summary>
        /// Finds the control under a screen point in the attached SAP session and issues a reference for it.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="point">The screen point in pixels.</param>
        /// <returns>200 with the control, or 404 with null when the point is not over a control of the session.</returns>
        (int StatusCode, SapElementAtModel ElementAt) FindElementAt(string session, SapPointModel point);

        /// <summary>
        /// Finds every control in the attached SAP session that matches a locator.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="locationStrategy">The W3C locator.</param>
        /// <returns>The matches in document order; empty when nothing matches.</returns>
        IEnumerable<SapElementModel> FindElements(string session, LocationStrategyModel locationStrategy);

        /// <summary>
        /// Finds every descendant of an element that matches a locator.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The parent element reference identifier.</param>
        /// <param name="locationStrategy">The W3C locator.</param>
        /// <returns>The matches in document order; empty when nothing matches.</returns>
        IEnumerable<SapElementModel> FindElements(string session, string element, LocationStrategyModel locationStrategy);

        /// <summary>
        /// Gets an issued element reference without touching SAP GUI.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The element reference identifier.</param>
        /// <returns>The element model, or null when the session or reference is unknown.</returns>
        SapElementModel GetElement(string session, string element);

        /// <summary>
        /// Gets a whitelisted attribute of a control.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The element reference identifier.</param>
        /// <param name="name">The attribute name.</param>
        /// <returns>200 with the value (empty when unknown or not exposed), or 404 when the element is stale.</returns>
        (int StatusCode, string Value) GetElementAttribute(string session, string element, string name);

        /// <summary>
        /// Gets the screen rectangle of a control.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The element reference identifier.</param>
        /// <returns>200 with the W3C rectangle, or 404 when the element is stale.</returns>
        (int StatusCode, ElementRectModel Rect) GetElementRect(string session, string element);

        /// <summary>
        /// Gets the visible text of a control.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The element reference identifier.</param>
        /// <returns>200 with the text, or 404 when the element is stale.</returns>
        (int StatusCode, string Text) GetElementText(string session, string element);

        /// <summary>
        /// Performs the SAP equivalent of a click on a control.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The element reference identifier.</param>
        /// <returns>200 when performed; 404 when the element is stale.</returns>
        int InvokeClick(string session, string element);

        /// <summary>
        /// Writes text to a control, replacing its content.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The element reference identifier.</param>
        /// <param name="text">The text to write.</param>
        /// <returns>200 when written; 404 when the element is stale.</returns>
        int SetValue(string session, string element, string text);
        #endregion
    }
}
