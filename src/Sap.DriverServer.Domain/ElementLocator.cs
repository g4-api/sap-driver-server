using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Evaluates W3C locators against SAP GUI Scripting objects and returns the matching scripting identifiers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="LocationStrategyModel.SapId"/> strategy and identifier CSS selectors resolve directly through
    /// <c>findById</c>. XPath, tag name, and the remaining CSS forms are first offered to <see cref="XpathResolver"/>,
    /// which walks the live tree for simple paths; other expressions are evaluated against the
    /// <see cref="DocumentObjectModelFactory"/> document of the search context (the session, or the parent element
    /// for element-relative searches). Both paths return the same matches in document order.
    /// </para>
    /// <para>
    /// The locator runs inside a scripting operation and returns COM-free <see cref="SapElementModel"/> instances
    /// without element reference identifiers; the elements repository issues those.
    /// </para>
    /// </remarks>
    public static class ElementLocator
    {
        #region *** Constants    ***
        // Matches the simple CSS forms Selenium generates: Type, *, [attr="v"], and Type[attr="v"].
        private static readonly Regex CssSelectorPattern = new(
            @"^\s*(?<type>\*|[A-Za-z][A-Za-z0-9_]*)?(?:\[\s*(?<attribute>[A-Za-z][A-Za-z0-9_-]*)\s*=\s*(?<quote>[""'])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>\s*\])?\s*$",
            RegexOptions.CultureInvariant);

        // Matches one CSS escape sequence, such as \/ or \[, which Selenium inserts into identifier selectors.
        private static readonly Regex CssEscapePattern = new(@"\\(.)", RegexOptions.CultureInvariant);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Finds the controls that match a locator within a search context.
        /// </summary>
        /// <param name="application">The <c>GuiApplication</c> node, used to resolve absolute identifiers.</param>
        /// <param name="context">The session or parent element node that scopes the search.</param>
        /// <param name="locator">The W3C locator.</param>
        /// <returns>The matches in document order; empty when nothing matches.</returns>
        /// <exception cref="SapDriverException">The strategy is unsupported or the selector is malformed.</exception>
        public static IList<SapElementModel> FindElements(ISapGuiNode application, ISapGuiNode context, LocationStrategyModel locator)
        {
            // Validate the locator shape before touching SAP GUI.
            AssertLocator(locator);

            // Dispatch on the strategy; identifier selectors skip the document build entirely.
            switch (locator.Using)
            {
                case LocationStrategyModel.SapId:
                    return FindById(application, context, locator.Value);
                case LocationStrategyModel.Xpath:
                    return FindByXpath(application, context, expression: locator.Value);
                case LocationStrategyModel.TagName:
                    return FindByXpath(application, context, expression: ConvertTagNameToXpath(locator.Value.Trim()));
                case LocationStrategyModel.CssSelector:
                    return FindByCssSelector(application, context, locator.Value);
                default:
                    var message = $"The locator strategy '{locator.Using}' is not supported by the SAP driver. " +
                        $"Use '{LocationStrategyModel.Xpath}', '{LocationStrategyModel.SapId}', " +
                        $"'{LocationStrategyModel.TagName}', or '{LocationStrategyModel.CssSelector}'.";
                    throw new SapDriverException(WebDriverErrorCodes.InvalidArgument, errorKind: null, StatusCodes.Status400BadRequest, message);
            }

            // Validates the locator members required by every strategy; no state is modified.
            static void AssertLocator(LocationStrategyModel locator)
            {
                // Require the locator object itself.
                ArgumentNullException.ThrowIfNull(argument: locator);

                // Require both members, which model validation normally guarantees for HTTP callers.
                if (string.IsNullOrWhiteSpace(locator.Using) || string.IsNullOrWhiteSpace(locator.Value))
                {
                    throw new SapDriverException(
                        WebDriverErrorCodes.InvalidArgument,
                        errorKind: null,
                        StatusCodes.Status400BadRequest,
                        message: "The locator requires both 'using' and 'value'.");
                }
            }
        }

        /// <summary>
        /// Converts a string to an XPath 1.0 string literal, using <c>concat()</c> when it contains both quote kinds.
        /// </summary>
        /// <param name="value">The literal text.</param>
        /// <returns>The XPath expression that evaluates to <paramref name="value"/>.</returns>
        public static string ConvertToXpathLiteral(string value)
        {
            // Prefer a single-quoted literal, then a double-quoted one.
            if (!value.Contains('\''))
            {
                return $"'{value}'";
            }

            if (!value.Contains('"'))
            {
                return $"\"{value}\"";
            }

            // Split on single quotes and rejoin them as double-quoted pieces inside concat().
            var parts = value
                .Split('\'')
                .Select(i => $"'{i}'");

            return $"concat({string.Join(", \"'\", ", parts)})";
        }

        // Converts a tag name to the descendant XPath used by the tag name strategy.
        // Valid XML names use the resolver-compatible /*//Type form; anything else uses local-name() on the document.
        private static string ConvertTagNameToXpath(string tagName)
        {
            // Only names that can be element names can match, so other values keep the literal comparison.
            var isElementName = string.Equals(DocumentObjectModelFactory.ConvertToElementName(tagName), tagName, StringComparison.Ordinal);

            return isElementName
                ? $"/*//{tagName}"
                : $"/*//*[local-name()={ConvertToXpathLiteral(tagName)}]";
        }

        // Evaluates a CSS selector: identifier selectors resolve through findById, the rest become XPath.
        // Unsupported CSS syntax is an invalid-selector error rather than an empty result.
        private static List<SapElementModel> FindByCssSelector(ISapGuiNode application, ISapGuiNode context, string selector)
        {
            // Resolve #id directly; SAP identifiers contain characters that clients usually leave unescaped.
            var trimmed = selector.Trim();
            if (trimmed.StartsWith('#'))
            {
                return FindById(application, context, id: CssEscapePattern.Replace(trimmed[1..], "$1"));
            }

            // Reject CSS that is not one of the simple supported forms.
            var match = CssSelectorPattern.Match(trimmed);
            var isSupported = match.Success && (match.Groups["type"].Success || match.Groups["attribute"].Success);
            if (!isSupported)
            {
                var message = $"The CSS selector '{selector}' is not supported. Use #id, Type, [attribute='value'], " +
                    "or Type[attribute='value'].";
                throw new SapDriverException(WebDriverErrorCodes.InvalidSelector, errorKind: null, StatusCodes.Status400BadRequest, message);
            }

            // Resolve [id='...'] directly, so relative identifiers work the same as with the id strategy.
            var type = match.Groups["type"].Success ? match.Groups["type"].Value : "*";
            var attribute = match.Groups["attribute"].Value;
            var value = CssEscapePattern.Replace(match.Groups["value"].Value, "$1");

            if (string.Equals(attribute, "id", StringComparison.OrdinalIgnoreCase))
            {
                return FindById(application, context, id: value)
                    .Where(i => type == "*" || string.Equals(i.Type, type, StringComparison.Ordinal))
                    .ToList();
            }

            // Translate the remaining forms to XPath over the object model document.
            var predicate = attribute.Length == 0
                ? string.Empty
                : $"[@{attribute}={ConvertToXpathLiteral(value)}]";

            return FindByXpath(application, context, expression: $"/*//{type}{predicate}");
        }

        // Resolves one identifier through findById: absolute identifiers from the application, relative ones from the context.
        private static List<SapElementModel> FindById(ISapGuiNode application, ISapGuiNode context, string id)
        {
            // Pick the resolution root by identifier form.
            var trimmed = id.Trim();
            var root = trimmed.StartsWith("/app/", StringComparison.OrdinalIgnoreCase) ? application : context;
            var node = root.FindById(trimmed);

            if (node is null)
            {
                return [];
            }

            return [new SapElementModel { SapId = node.Id, Type = node.Type }];
        }

        // Evaluates an XPath expression: the live-tree resolver handles its grammar, everything else uses the document.
        // Both paths return the same matches; expressions that fail to parse, or that select non-element nodes, are
        // invalid-selector errors.
        private static List<SapElementModel> FindByXpath(ISapGuiNode application, ISapGuiNode context, string expression)
        {
            // Resolve simple paths on the live tree, reading only the objects and properties the path needs.
            var steps = XpathResolver.ConvertToSteps(expression);
            if (steps is not null)
            {
                return [.. XpathResolver.FindElements(application, context, steps)];
            }

            // Build the document for the search context; for element searches the parent is the document root.
            var document = DocumentObjectModelFactory.NewDocument(context);

            IEnumerable<XElement> elements;
            try
            {
                // Evaluate eagerly so syntax errors surface here rather than during enumeration.
                elements = document.XPathSelectElements(expression).ToList();
            }
            catch (Exception exception) when (exception is XPathException or InvalidOperationException)
            {
                var message = $"The XPath expression '{expression}' is invalid or does not select elements.";
                throw new SapDriverException(
                    WebDriverErrorCodes.InvalidSelector,
                    errorKind: null,
                    StatusCodes.Status400BadRequest,
                    message,
                    data: null,
                    innerException: exception);
            }

            // Map the matches back to scripting identifiers, skipping elements without an identifier.
            return elements
                .Select(i => new SapElementModel { SapId = (string)i.Attribute("id"), Type = (string)i.Attribute("type") })
                .Where(i => !string.IsNullOrEmpty(i.SapId))
                .ToList();
        }
        #endregion
    }
}
