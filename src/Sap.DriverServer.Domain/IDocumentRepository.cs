using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Produces the XML object model document of a SAP GUI session or element.
    /// </summary>
    public interface IDocumentRepository
    {
        #region *** Methods      ***
        /// <summary>
        /// Gets the object model document of an element's subtree.
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <param name="element">The element model returned by a locator.</param>
        /// <returns>200 with the XML text, or 404 when the session is unknown or the control no longer exists.</returns>
        (int StatusCode, string Result) GetElementSource(string session, SapElementModel element);

        /// <summary>
        /// Gets the object model document of the attached SAP session (all of its windows).
        /// </summary>
        /// <param name="session">The session identifier.</param>
        /// <returns>200 with the XML text, or 404 when the session is unknown.</returns>
        (int StatusCode, string Result) GetPageSource(string session);
        #endregion
    }
}
