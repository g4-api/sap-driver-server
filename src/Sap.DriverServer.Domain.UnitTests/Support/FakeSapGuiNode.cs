using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Sap.DriverServer.Domain;

namespace Sap.DriverServer.Domain.UnitTests.Support
{
    /// <summary>
    /// In-memory <see cref="ISapGuiNode"/> that models one SAP GUI Scripting object for tests without SAP GUI.
    /// </summary>
    /// <remarks>
    /// The fake follows the production contract: absent properties and methods raise a
    /// <see cref="SapDriverException"/> tagged <c>notExposedByScripting</c>, <c>findById</c> resolves absolute
    /// identifiers from the tree root and relative identifiers from the node, and <c>Children</c> keeps insertion order.
    /// Every method invocation and property assignment is recorded in <see cref="Invocations"/>.
    /// </remarks>
    public sealed class FakeSapGuiNode : ISapGuiNode
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="FakeSapGuiNode"/> class.
        /// </summary>
        /// <param name="id">The absolute SAP GUI Scripting identifier.</param>
        /// <param name="type">The SAP GUI Scripting type name.</param>
        public FakeSapGuiNode(string id, string type)
        {
            // Store identity separately so tests cannot remove it through the property bag.
            Id = id;
            Type = type;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the child nodes in SAP order.
        /// </summary>
        public List<FakeSapGuiNode> Children { get; } = [];

        /// <summary>
        /// Gets the number of times the <c>Children</c> collection was read, so tests can prove which subtrees were walked.
        /// </summary>
        public int ChildrenReads { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the node exposes a <c>Children</c> collection.
        /// </summary>
        public bool IsContainer { get; set; } = true;

        /// <inheritdoc />
        public string Id { get; }

        /// <summary>
        /// Gets the recorded invocations, such as <c>press()</c>, <c>sendVKey(0)</c>, or <c>set Text=abc</c>.
        /// </summary>
        public List<string> Invocations { get; } = [];

        /// <summary>
        /// Gets the names of the properties read through <see cref="GetProperty"/>, in read order.
        /// </summary>
        public List<string> PropertyReads { get; } = [];

        /// <summary>
        /// Gets the method handlers; methods without a handler are recorded and return null.
        /// </summary>
        public Dictionary<string, Func<object[], object>> Methods { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the parent node, or null for the tree root.
        /// </summary>
        public FakeSapGuiNode Parent { get; private set; }

        /// <summary>
        /// Gets the exposed properties; object-valued properties hold <see cref="FakeSapGuiNode"/> instances.
        /// </summary>
        public Dictionary<string, object> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the names of the methods the node exposes; null means every method is exposed.
        /// </summary>
        public HashSet<string> SupportedMethods { get; set; }

        /// <inheritdoc />
        public string Type { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Adds a child node and returns it, so trees can be built fluently.
        /// </summary>
        /// <param name="child">The child to add.</param>
        /// <returns>The added child.</returns>
        public FakeSapGuiNode Add(FakeSapGuiNode child)
        {
            // Link both directions so absolute findById calls can reach the root.
            child.Parent = this;
            Children.Add(child);

            return child;
        }

        /// <inheritdoc />
        public ISapGuiNode FindById(string id)
        {
            // Resolve absolute identifiers from the root and relative ones below this node.
            var isAbsolute = id.StartsWith("/app", StringComparison.OrdinalIgnoreCase);
            var target = isAbsolute ? id : $"{Id}/{id}";

            return GetRoot()
                .GetSelfAndDescendants()
                .FirstOrDefault(i => string.Equals(i.Id, target, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public IReadOnlyList<ISapGuiNode> GetChildren()
        {
            // Count the read, then mirror the production behavior for leaves, which expose no Children collection.
            ChildrenReads++;

            return IsContainer ? Children : [];
        }

        /// <inheritdoc />
        public ISapGuiNode GetNode(string propertyName)
        {
            // Return object-valued properties; absent ones raise the "not exposed" failure.
            return GetProperty(propertyName) as ISapGuiNode;
        }

        /// <inheritdoc />
        public object GetProperty(string propertyName)
        {
            // Record the read so tests can prove sensitive properties were never requested.
            PropertyReads.Add(propertyName);

            // Serve identity from the dedicated members and everything else from the property bag.
            if (string.Equals(propertyName, "Id", StringComparison.OrdinalIgnoreCase))
            {
                return Id;
            }

            if (string.Equals(propertyName, "Type", StringComparison.OrdinalIgnoreCase))
            {
                return Type;
            }

            if (Properties.TryGetValue(propertyName, out var value))
            {
                return value;
            }

            // Derive the structural properties every SAP object exposes from the tree shape.
            if (string.Equals(propertyName, "Parent", StringComparison.OrdinalIgnoreCase))
            {
                return Parent;
            }

            if (string.Equals(propertyName, "ContainerType", StringComparison.OrdinalIgnoreCase))
            {
                return IsContainer;
            }

            throw SapDriverException.NewNotExposed($"SAP GUI Scripting does not expose '{propertyName}' on this object.");
        }

        /// <summary>
        /// Gets this node and all of its descendants in document order.
        /// </summary>
        /// <returns>The nodes in depth-first order.</returns>
        public IEnumerable<FakeSapGuiNode> GetSelfAndDescendants()
        {
            // Walk depth-first so the order matches the object model document.
            yield return this;

            foreach (var descendant in Children.SelectMany(i => i.GetSelfAndDescendants()))
            {
                yield return descendant;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<object> InvokeCollectionMethod(string methodName, params object[] arguments)
        {
            // Run the method like InvokeMethod and expose an enumerable result as the collection items.
            var result = InvokeMethod(methodName, arguments);

            return result is System.Collections.IEnumerable items and not string ? items.Cast<object>().ToList() : [];
        }

        /// <inheritdoc />
        public object InvokeMethod(string methodName, params object[] arguments)
        {
            // Reject methods outside the supported set, like IDispatch does.
            if (SupportedMethods is not null && !SupportedMethods.Contains(methodName))
            {
                throw SapDriverException.NewNotExposed($"SAP GUI Scripting does not expose '{methodName}' on this object.");
            }

            // Record the call with its arguments, then run the handler when one is configured.
            var argumentText = string.Join(",", (arguments ?? []).Select(i => Convert.ToString(i, CultureInfo.InvariantCulture)));
            Invocations.Add($"{methodName}({argumentText})");

            return Methods.TryGetValue(methodName, out var handler) ? handler(arguments) : null;
        }

        /// <inheritdoc />
        public void SetProperty(string propertyName, object value)
        {
            // Record and apply the assignment.
            Invocations.Add($"set {propertyName}={Convert.ToString(value, CultureInfo.InvariantCulture)}");
            Properties[propertyName] = value;
        }

        /// <summary>
        /// Sets one property and returns the node, so test trees can be configured fluently.
        /// </summary>
        /// <param name="propertyName">The SAP GUI Scripting property name.</param>
        /// <param name="value">The property value.</param>
        /// <returns>This node.</returns>
        public FakeSapGuiNode With(string propertyName, object value)
        {
            // Write the property bag directly; unlike SetProperty this is fixture setup and is not recorded.
            Properties[propertyName] = value;

            return this;
        }

        // Walks up to the tree root, which owns absolute identifier resolution.
        private FakeSapGuiNode GetRoot()
        {
            var node = this;
            while (node.Parent is not null)
            {
                node = node.Parent;
            }

            return node;
        }
        #endregion
    }
}
