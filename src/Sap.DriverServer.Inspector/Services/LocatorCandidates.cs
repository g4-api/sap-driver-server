using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Builds the driver locators the inspector proposes for a control, and the identifier parts they depend on.
    /// </summary>
    /// <remarks>
    /// Both inspection sources build the same candidates from the same page-source attributes, so a locator copied
    /// from local mode behaves the same in a driver session. Verification (match counting) belongs to each source.
    /// </remarks>
    public static class LocatorCandidates
    {
        #region *** Constants    ***
        // Lists the page-source attributes that become XPath candidates, in proposal order.
        private static readonly string[] CandidateAttributes = ["name", "text", "tooltip"];

        // Captures the session part of a scripting identifier, such as /app/con[0]/ses[1].
        private static readonly Regex SessionIdPattern = new(@"^/app/con\[\d+\]/ses\[\d+\]", RegexOptions.CultureInvariant);

        // Captures the frame window part of a scripting identifier, such as /app/con[0]/ses[1]/wnd[0].
        private static readonly Regex WindowIdPattern = new(@"^/app/con\[\d+\]/ses\[\d+\]/wnd\[\d+\]", RegexOptions.CultureInvariant);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the session part of an identifier.
        /// </summary>
        /// <param name="id">The absolute scripting identifier.</param>
        /// <returns>The session identifier, such as <c>/app/con[0]/ses[0]</c>; empty above session level.</returns>
        public static string GetSessionId(string id)
        {
            // Match the leading /app/con[n]/ses[n] part; null identifiers match nothing.
            var match = SessionIdPattern.Match(id ?? string.Empty);
            return match.Success ? match.Value : string.Empty;
        }

        /// <summary>
        /// Gets the frame window part of an identifier.
        /// </summary>
        /// <param name="id">The absolute scripting identifier.</param>
        /// <returns>The window identifier, such as <c>/app/con[0]/ses[0]/wnd[1]</c>; empty above window level.</returns>
        public static string GetWindowId(string id)
        {
            // Match the leading /app/con[n]/ses[n]/wnd[n] part; null identifiers match nothing.
            var match = WindowIdPattern.Match(id ?? string.Empty);
            return match.Success ? match.Value : string.Empty;
        }

        /// <summary>
        /// Builds the locator candidates for a control: relative id, name, text, tooltip, and absolute-id XPath.
        /// </summary>
        /// <param name="id">The absolute identifier of the control.</param>
        /// <param name="type">The SAP type of the control.</param>
        /// <param name="getAttribute">Reads one page-source attribute (<c>name</c>, <c>text</c>, or <c>tooltip</c>); empty when absent.</param>
        /// <returns>The unverified candidates, most specific last; empty above session level.</returns>
        public static IList<LocatorCandidate> NewCandidates(string id, string type, Func<string, string> getAttribute)
        {
            // Locators are session-relative, so objects at or above session level get none.
            var sessionId = GetSessionId(id);
            if (sessionId.Length == 0 || id.Length <= sessionId.Length + 1)
            {
                return [];
            }

            // Start with the session-relative id, the strategy recorded scripts use.
            var elementName = DocumentObjectModelFactory.ConvertToElementName(type);
            var candidates = new List<LocatorCandidate>
            {
                new() { Strategy = LocationStrategyModel.SapId, TargetId = id, Value = id[(sessionId.Length + 1)..] }
            };

            // Add one XPath per non-empty display attribute; the reader never returns password text.
            foreach (var attribute in CandidateAttributes)
            {
                var value = getAttribute(attribute);
                if (!string.IsNullOrEmpty(value))
                {
                    var xpath = $"//{elementName}[@{attribute}={ElementLocator.ConvertToXpathLiteral(value)}]";
                    candidates.Add(new LocatorCandidate { Strategy = LocationStrategyModel.Xpath, TargetId = id, Value = xpath });
                }
            }

            // Add the absolute-id XPath last; it is always unique and resolves without walking the tree.
            candidates.Add(new LocatorCandidate { Strategy = LocationStrategyModel.Xpath, TargetId = id, Value = NewIdXpath(id, type) });

            return candidates;
        }

        /// <summary>
        /// Builds the absolute-id XPath of a control, which the driver resolves directly through <c>findById</c>.
        /// </summary>
        /// <param name="id">The absolute identifier.</param>
        /// <param name="type">The SAP type.</param>
        /// <returns>An XPath such as <c>//GuiButton[@id='/app/con[0]/ses[0]/wnd[0]/tbar[0]/btn[3]']</c>.</returns>
        public static string NewIdXpath(string id, string type)
        {
            // Name the element after the type so the expression stays readable and type-checked.
            return $"//{DocumentObjectModelFactory.ConvertToElementName(type)}[@id={ElementLocator.ConvertToXpathLiteral(id)}]";
        }
        #endregion
    }
}
