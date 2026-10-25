/*
 * RESOURCES
 * https://www.w3.org/TR/1999/REC-xpath-19991116/
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Resolves a subset of XPath directly against the live SAP GUI Scripting tree, without building the object model document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the SAP counterpart of the UIA server's segment parser. SAP GUI Scripting has no query API, so the
    /// resolver walks <c>Children</c> step by step and reads only the type of each visited object and the
    /// properties its predicates need. Absolute child paths touch only the objects on the path; descendant steps
    /// read the subtree below the step, but far fewer properties per object than the full document.
    /// </para>
    /// <para>
    /// Supported grammar: an optional leading <c>.</c>, then one or more steps of <c>/</c> (child) or <c>//</c>
    /// (descendant) followed by a SAP type or <c>*</c>, each with any number of <c>[@attribute='value']</c> or
    /// <c>[n]</c> predicates. Attribute names are the document attributes (<c>id</c>, <c>type</c>, <c>name</c>,
    /// <c>text</c>, <c>tooltip</c>, <c>changeable</c>, <c>subType</c>). <c>//Type[@id='/app/…']</c> resolves through
    /// <c>findById</c> without walking the tree.
    /// </para>
    /// <para>
    /// Results equal the XPath 1.0 evaluation of the same expression over the <see cref="DocumentObjectModelFactory"/>
    /// document: the same element names, attribute inclusion rules, depth limit, per-parent positional predicates,
    /// and document order. Expressions outside the grammar make <see cref="ConvertToSteps"/> return null, and the
    /// caller falls back to the document.
    /// </para>
    /// </remarks>
    public static class XpathResolver
    {
        #region *** Constants    ***
        // Matches an attribute name at the current parser position.
        private static readonly Regex AttributePattern = new(@"\G[A-Za-z][A-Za-z0-9_]*", RegexOptions.CultureInvariant);

        // Matches a node test at the current parser position: * or an ASCII XML name such as GuiButton.
        private static readonly Regex NodeTestPattern = new(@"\G(\*|[A-Za-z_][A-Za-z0-9_.-]*)", RegexOptions.CultureInvariant);

        // Matches a positive integer position at the current parser position.
        private static readonly Regex PositionPattern = new(@"\G[0-9]+", RegexOptions.CultureInvariant);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Parses an XPath expression into resolver steps.
        /// </summary>
        /// <param name="expression">The XPath expression.</param>
        /// <returns>The steps, or null when the expression is outside the supported grammar.</returns>
        public static IList<XpathStep> ConvertToSteps(string expression)
        {
            // Treat absent expressions as unsupported so the document path reports them.
            if (string.IsNullOrEmpty(expression))
            {
                return null;
            }

            // Accept a leading "." before a slash; on the document it denotes the same starting point.
            var index = expression.StartsWith("./", StringComparison.Ordinal) ? 1 : 0;
            var steps = new List<XpathStep>();

            while (index < expression.Length)
            {
                // Read one step; any parse failure means the expression is outside the grammar.
                var step = ReadStep(expression, ref index);
                if (step is null)
                {
                    return null;
                }

                steps.Add(step);
            }

            return steps.Count == 0 ? null : steps;
        }

        /// <summary>
        /// Resolves parsed steps against the live tree below a search context.
        /// </summary>
        /// <param name="application">The <c>GuiApplication</c> node, used by the identifier shortcut.</param>
        /// <param name="context">The session or parent element node that is the root of the virtual document.</param>
        /// <param name="steps">The steps returned by <see cref="ConvertToSteps"/>.</param>
        /// <returns>The matches in document order.</returns>
        public static IList<SapElementModel> FindElements(ISapGuiNode application, ISapGuiNode context, IList<XpathStep> steps)
        {
            // Resolve //Type[@id='/app/…'] through findById, skipping the tree walk entirely.
            if (TestIdShortcut(steps))
            {
                return FindById(application, context, steps[0]);
            }

            // Start at the virtual document node, whose only child is the context node.
            var current = new List<Candidate> { Candidate.NewDocument(context) };

            foreach (var step in steps)
            {
                // Apply the step to every node of the current set, then restore document order without duplicates.
                var next = new List<Candidate>();
                foreach (var candidate in current)
                {
                    if (step.IsDescendant)
                    {
                        AddDescendantMatches(candidate, step, next);
                    }
                    else
                    {
                        next.AddRange(SelectMatches(candidate.GetChildren(), step));
                    }
                }

                current = SortCandidates(next);
            }

            // Map the matches to COM-free models.
            return current
                .Select(i => new SapElementModel { SapId = i.Node.Id, Type = i.Type })
                .ToList();
        }

        // Applies a descendant step: the step's node test and predicates run on the children of the candidate and of
        // every descendant, which is XPath's descendant-or-self::node()/child::test. Each object's children are read once.
        private static void AddDescendantMatches(Candidate candidate, XpathStep step, List<Candidate> results)
        {
            // Match among this node's children, then recurse into each child in SAP order.
            var children = candidate.GetChildren();
            results.AddRange(SelectMatches(children, step));

            foreach (var child in children)
            {
                AddDescendantMatches(child, step, results);
            }
        }

        // Resolves the identifier shortcut and applies the same node test and subtree rule as the document.
        private static List<SapElementModel> FindById(ISapGuiNode application, ISapGuiNode context, XpathStep step)
        {
            // Document ids are absolute, so a relative value never matches.
            var id = step.Predicates[0].Value;
            var node = id.StartsWith("/app/", StringComparison.Ordinal) ? application.FindById(id) : null;
            if (node is null || !string.Equals(node.Id, id, StringComparison.Ordinal))
            {
                return [];
            }

            // Keep the match only inside the context subtree and only when the node test accepts its type.
            var type = node.Type;
            var isInContext = string.Equals(node.Id, context.Id, StringComparison.Ordinal)
                || node.Id.StartsWith($"{context.Id}/", StringComparison.Ordinal);
            var isTypeMatch = step.NodeTest == "*" || step.NodeTest == DocumentObjectModelFactory.ConvertToElementName(type);

            return isInContext && isTypeMatch ? [new SapElementModel { SapId = node.Id, Type = type }] : [];
        }

        // Consumes an expected character at the parser position; returns false without consuming when it differs.
        private static bool ReadCharacter(string expression, ref int index, char expected)
        {
            // Advance only on a match so the caller can report the failure.
            if (index < expression.Length && expression[index] == expected)
            {
                index++;
                return true;
            }

            return false;
        }

        // Reads a quoted string literal at the parser position; returns null when unquoted or unterminated.
        private static string ReadLiteral(string expression, ref int index)
        {
            // Accept single or double quotes; XPath 1.0 literals have no escapes.
            if (index >= expression.Length || (expression[index] != '\'' && expression[index] != '"'))
            {
                return null;
            }

            var quote = expression[index];
            var end = expression.IndexOf(quote, index + 1);
            if (end < 0)
            {
                return null;
            }

            var literal = expression[(index + 1)..end];
            index = end + 1;

            return literal;
        }

        // Reads one predicate at the parser position: [n] or [@attribute='value']; returns null when malformed.
        private static XpathPredicate ReadPredicate(string expression, ref int index)
        {
            // Read a positional predicate; position 0 never matches in XPath and is left to the document path.
            index++;
            var position = PositionPattern.Match(expression, index);
            if (position.Success)
            {
                index += position.Length;
                var value = int.Parse(position.Value, CultureInfo.InvariantCulture);

                return value > 0 && ReadCharacter(expression, ref index, ']') ? new XpathPredicate { Position = value } : null;
            }

            // Read an attribute equality; only document attributes are supported.
            if (!ReadCharacter(expression, ref index, '@'))
            {
                return null;
            }

            var attribute = AttributePattern.Match(expression, index);
            if (!attribute.Success || !DocumentObjectModelFactory.TestAttributeName(attribute.Value))
            {
                return null;
            }

            index += attribute.Length;
            SkipSpaces(expression, ref index);
            if (!ReadCharacter(expression, ref index, '='))
            {
                return null;
            }

            SkipSpaces(expression, ref index);
            var literal = ReadLiteral(expression, ref index);
            SkipSpaces(expression, ref index);

            return literal is not null && ReadCharacter(expression, ref index, ']')
                ? new XpathPredicate { Attribute = attribute.Value, Value = literal }
                : null;
        }

        // Reads one step at the parser position: an axis, a node test, and its predicates; returns null when malformed.
        private static XpathStep ReadStep(string expression, ref int index)
        {
            // Read the axis: "//" selects descendants, "/" selects children.
            var isDescendant = string.CompareOrdinal(expression, index, "//", 0, 2) == 0;
            if (!isDescendant && expression[index] != '/')
            {
                return null;
            }

            index += isDescendant ? 2 : 1;

            // Read the node test.
            var nodeTest = NodeTestPattern.Match(expression, index);
            if (!nodeTest.Success)
            {
                return null;
            }

            index += nodeTest.Length;
            var step = new XpathStep { IsDescendant = isDescendant, NodeTest = nodeTest.Value };

            // Read the predicates in order; XPath applies them left to right.
            while (index < expression.Length && expression[index] == '[')
            {
                var predicate = ReadPredicate(expression, ref index);
                if (predicate is null)
                {
                    return null;
                }

                step.Predicates.Add(predicate);
            }

            return step;
        }

        // Applies a step's node test and predicates to one parent's children, as XPath does per context node.
        private static List<Candidate> SelectMatches(IReadOnlyList<Candidate> children, XpathStep step)
        {
            // Filter by node test, then apply each predicate to the sequence the previous one produced.
            var matches = children
                .Where(i => step.NodeTest == "*" || step.NodeTest == i.ElementName)
                .ToList();

            foreach (var predicate in step.Predicates)
            {
                matches = predicate.Position > 0
                    ? matches.Skip(predicate.Position - 1).Take(1).ToList()
                    : matches.Where(i => TestAttribute(i, predicate)).ToList();
            }

            return matches;
        }

        // Skips spaces around "=" inside a predicate.
        private static void SkipSpaces(string expression, ref int index)
        {
            // Advance past insignificant whitespace.
            while (index < expression.Length && char.IsWhiteSpace(expression[index]))
            {
                index++;
            }
        }

        // Sorts candidates into document order and removes duplicates reached through different ancestors.
        private static List<Candidate> SortCandidates(List<Candidate> candidates)
        {
            // Child-index paths compare lexicographically in document order, with ancestors before descendants.
            return candidates
                .GroupBy(i => i.PathKey, StringComparer.Ordinal)
                .Select(i => i.First())
                .OrderBy(i => i.Path, PathComparer.Instance)
                .ToList();
        }

        // Tests an attribute predicate using the document's inclusion rules; absent attributes never match.
        private static bool TestAttribute(Candidate candidate, XpathPredicate predicate)
        {
            // Compare ordinally, as XPath string equality does.
            var value = DocumentObjectModelFactory.GetAttributeValue(candidate.Node, candidate.Type, predicate.Attribute);

            return !string.IsNullOrEmpty(value) && string.Equals(value, predicate.Value, StringComparison.Ordinal);
        }

        // Tests whether the steps are exactly //Type[@id='…'], which resolves through findById.
        private static bool TestIdShortcut(IList<XpathStep> steps)
        {
            // Require one descendant step with a single id equality predicate.
            return steps.Count == 1
                && steps[0].IsDescendant
                && steps[0].Predicates.Count == 1
                && steps[0].Predicates[0].Attribute == "id";
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents one predicate of a resolver step.
        /// </summary>
        public class XpathPredicate
        {
            /// <summary>
            /// Gets or sets the document attribute compared by an equality predicate; null for a positional predicate.
            /// </summary>
            public string Attribute { get; set; }

            /// <summary>
            /// Gets or sets the one-based position selected by a positional predicate; 0 for an equality predicate.
            /// </summary>
            public int Position { get; set; }

            /// <summary>
            /// Gets or sets the literal compared by an equality predicate.
            /// </summary>
            public string Value { get; set; }
        }

        /// <summary>
        /// Represents one location step: an axis, a node test, and its predicates.
        /// </summary>
        public class XpathStep
        {
            /// <summary>
            /// Gets or sets a value indicating whether the step uses <c>//</c> (descendants) rather than <c>/</c> (children).
            /// </summary>
            public bool IsDescendant { get; set; }

            /// <summary>
            /// Gets or sets the node test: a document element name, such as <c>GuiButton</c>, or <c>*</c>.
            /// </summary>
            public string NodeTest { get; set; }

            /// <summary>
            /// Gets the predicates in the order XPath applies them.
            /// </summary>
            public IList<XpathPredicate> Predicates { get; } = [];
        }

        // One visited object with its document position; the virtual document node has no SAP node.
        // Children are read lazily and at most once, and never beyond the document depth limit.
        private sealed class Candidate
        {
            // Holds the search context, which is the only child of the virtual document node.
            private readonly ISapGuiNode _context;

            // Caches the children so each object's Children collection is read once per resolution.
            private IReadOnlyList<Candidate> _children;

            private Candidate(ISapGuiNode node, ISapGuiNode context, int[] path)
            {
                // Read the type once; it drives the node test and the attribute inclusion rules.
                Node = node;
                _context = context;
                Path = path;
                PathKey = string.Join(".", path);
                Type = node?.Type;
                ElementName = node is null ? null : DocumentObjectModelFactory.ConvertToElementName(Type);
            }

            // The document element name the node test compares; null for the virtual document node.
            public string ElementName { get; }

            // The visited SAP node; null for the virtual document node.
            public ISapGuiNode Node { get; }

            // The child-index path from the document node, which orders candidates in document order.
            public int[] Path { get; }

            // The path as a string, used to remove candidates reached through different ancestors.
            public string PathKey { get; }

            // The SAP type, read once per visited node.
            public string Type { get; }

            // Creates the virtual document node above the search context.
            public static Candidate NewDocument(ISapGuiNode context)
            {
                // The document node sits above the context at an empty path.
                return new Candidate(node: null, context, path: []);
            }

            // Gets the child candidates, reading the SAP Children collection at most once.
            public IReadOnlyList<Candidate> GetChildren()
            {
                // Return the cached children after the first read.
                if (_children is not null)
                {
                    return _children;
                }

                // The document node's only child is the context; elements at the depth limit have no children.
                if (Node is null)
                {
                    _children = [new Candidate(_context, _context, path: [0])];
                }
                else if (Path.Length - 1 >= DocumentObjectModelFactory.MaximumDepth)
                {
                    _children = [];
                }
                else
                {
                    _children = Node
                        .GetChildren()
                        .Select((child, index) => new Candidate(child, _context, path: [.. Path, index]))
                        .ToList();
                }

                return _children;
            }
        }

        // Orders child-index paths lexicographically, which is document order.
        private sealed class PathComparer : IComparer<int[]>
        {
            // Shares one stateless instance across sorts.
            public static readonly PathComparer Instance = new();

            // Compares two paths index by index.
            public int Compare(int[] x, int[] y)
            {
                // Compare index by index; a shorter prefix (an ancestor) sorts first.
                var length = Math.Min(x.Length, y.Length);
                for (var i = 0; i < length; i++)
                {
                    if (x[i] != y[i])
                    {
                        return x[i].CompareTo(y[i]);
                    }
                }

                return x.Length.CompareTo(y.Length);
            }
        }
        #endregion
    }
}
