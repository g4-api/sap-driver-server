using System;
using System.Xml;
using System.Xml.Linq;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Builds the XML object model document that XPath and tag name locators, and the page source endpoint, operate on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each SAP GUI Scripting object becomes one element named after its <c>Type</c>, such as <c>GuiButton</c>, with
    /// the attributes <c>id</c>, <c>name</c>, <c>type</c>, <c>text</c>, <c>tooltip</c>, <c>changeable</c>, and,
    /// for shells, <c>subType</c>. The <c>id</c> attribute is the absolute scripting identifier that element
    /// references resolve through <c>findById</c>.
    /// </para>
    /// <para>
    /// The document mirrors only what SAP GUI Scripting exposes: shell controls such as grids and trees appear
    /// as single <c>GuiShell</c> elements because their rows and nodes are not scripting objects. Password field
    /// text is never read. Building the document costs several cross-process calls per object, so it runs once
    /// per locator or source request and is not cached across requests, which keeps results current.
    /// </para>
    /// <para>
    /// The element-name, attribute, and depth rules are shared with <see cref="XpathResolver"/>, so the live-tree
    /// fast path and the document evaluate every supported expression to the same result.
    /// </para>
    /// </remarks>
    public static class DocumentObjectModelFactory
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the deepest element level that receives children; deeper objects are not part of the document.
        /// </summary>
        /// <remarks>
        /// The guard stops runaway recursion if SAP GUI ever reports a cyclic or extremely deep tree.
        /// </remarks>
        public const int MaximumDepth = 64;

        // Names elements whose SAP type is missing or not a valid XML name.
        private const string FallbackElementName = "GuiComponent";

        // Lists the document attributes in the order they are written to each element.
        private static readonly string[] AttributeNames = ["id", "type", "name", "text", "tooltip", "changeable", "subType"];
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Converts a SAP type name to the XML element name used in the document.
        /// </summary>
        /// <param name="type">The SAP GUI Scripting type, such as <c>GuiButton</c>.</param>
        /// <returns>The type itself when it is a valid XML name; otherwise <c>GuiComponent</c>.</returns>
        public static string ConvertToElementName(string type)
        {
            // SAP type names are plain identifiers; guard anyway so an unexpected value cannot break the document.
            if (string.IsNullOrWhiteSpace(type))
            {
                return FallbackElementName;
            }

            try
            {
                return XmlConvert.VerifyName(type);
            }
            catch (XmlException)
            {
                return FallbackElementName;
            }
        }

        /// <summary>
        /// Gets the value a document attribute has for a node, applying the document's inclusion rules.
        /// </summary>
        /// <remarks>
        /// Sessions carry only identity attributes, password fields never expose <c>text</c>, and only shells carry
        /// <c>subType</c>. An empty result means the attribute is absent from the element.
        /// </remarks>
        /// <param name="node">The node to read.</param>
        /// <param name="type">The node's SAP type, passed in so callers read it only once.</param>
        /// <param name="attributeName">The document attribute name, such as <c>text</c>.</param>
        /// <returns>The attribute value, an empty string when absent, or null when the name is not a document attribute.</returns>
        public static string GetAttributeValue(ISapGuiNode node, string type, string attributeName)
        {
            // Classify the node once; the rules below depend only on its type.
            var isSession = string.Equals(type, SapGuiTypes.Session, StringComparison.Ordinal);
            var isPassword = string.Equals(type, SapGuiTypes.PasswordField, StringComparison.Ordinal);
            var isShell = string.Equals(type, SapGuiTypes.Shell, StringComparison.Ordinal);

            // Read only the property the attribute needs, so the fast path avoids unrelated cross-process calls.
            return attributeName switch
            {
                "id" => node.Id ?? string.Empty,
                "type" => type ?? string.Empty,
                "name" => SapGuiNavigator.GetString(node, propertyName: "Name"),
                "text" => isSession || isPassword ? string.Empty : SapGuiNavigator.GetString(node, propertyName: "Text"),
                "tooltip" => isSession ? string.Empty : SapGuiNavigator.GetString(node, propertyName: "Tooltip"),
                "changeable" => isSession ? string.Empty : SapGuiNavigator.GetString(node, propertyName: "Changeable"),
                "subType" => isShell ? SapGuiNavigator.GetString(node, propertyName: "SubType") : string.Empty,
                _ => null
            };
        }

        /// <summary>
        /// Builds the object model document rooted at the specified node.
        /// </summary>
        /// <param name="root">The session, window, or control node that becomes the document root.</param>
        /// <returns>The object model document.</returns>
        public static XDocument NewDocument(ISapGuiNode root)
        {
            // Require a root; the caller resolves it inside the current scripting operation.
            ArgumentNullException.ThrowIfNull(argument: root);

            // Convert the tree recursively, starting at depth zero.
            return new XDocument(NewElement(root, depth: 0));
        }

        /// <summary>
        /// Tests whether a document attribute name is one the document writes.
        /// </summary>
        /// <param name="attributeName">The attribute name, compared case-sensitively as XPath does.</param>
        /// <returns>True for <c>id</c>, <c>type</c>, <c>name</c>, <c>text</c>, <c>tooltip</c>, <c>changeable</c>, and <c>subType</c>.</returns>
        public static bool TestAttributeName(string attributeName)
        {
            // XPath attribute names are case-sensitive, so the comparison is ordinal.
            return Array.IndexOf(AttributeNames, attributeName) >= 0;
        }

        // Converts one node and, recursively, its children into an XML element.
        // Reads identifiers and display properties only; the node tree is not modified.
        private static XElement NewElement(ISapGuiNode node, int depth)
        {
            // Name the element after the SAP type so XPath expressions use SAP vocabulary.
            var type = node.Type;
            var element = new XElement(ConvertToElementName(type));

            // Record each attribute the inclusion rules allow; empty values are omitted to keep the document compact.
            foreach (var attributeName in AttributeNames)
            {
                var value = GetAttributeValue(node, type, attributeName);
                if (!string.IsNullOrEmpty(value))
                {
                    element.SetAttributeValue(attributeName, value);
                }
            }

            // Stop descending at the depth guard; the truncated element still carries its identity.
            if (depth >= MaximumDepth)
            {
                return element;
            }

            // Append the children in SAP order so positional XPath predicates follow the scripting tree.
            foreach (var child in node.GetChildren())
            {
                element.Add(NewElement(child, depth + 1));
            }

            return element;
        }
        #endregion
    }
}
