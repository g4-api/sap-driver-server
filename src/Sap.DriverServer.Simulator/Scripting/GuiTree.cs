using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's tree shell (<c>GuiShell</c> with <c>SubType</c> <c>Tree</c>), such as the SAP Easy Access menu.
    /// </summary>
    /// <remarks>
    /// Nodes are addressed by key; each tree item's tag holds its key. Unknown keys raise an error.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiTree : GuiShell
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTree"/> class.
        /// </summary>
        /// <param name="element">The tree view.</param>
        public GuiTree(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the key of the selected node; empty when no node is selected.
        /// </summary>
        public string SelectedNode => (Tree.SelectedItem as TreeViewItem)?.Tag as string ?? string.Empty;

        /// <inheritdoc />
        public override string SubType => "Tree";

        // Gets the wrapped tree view.
        private TreeView Tree => (TreeView)Element;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Collapses a node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        public void CollapseNode(string nodeKey)
        {
            // Collapse the node; an unknown key raises an error.
            GetNode(nodeKey).IsExpanded = false;
        }

        /// <summary>
        /// Selects a node and double-clicks it, which starts the node's transaction in SAP Easy Access.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        public void DoubleClickNode(string nodeKey)
        {
            // Select the node first, because the double-click handler reads the selection.
            SelectNode(nodeKey);
            RaiseDoubleClick();
        }

        /// <summary>
        /// Expands a node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        public void ExpandNode(string nodeKey)
        {
            // Expand the node; an unknown key raises an error.
            GetNode(nodeKey).IsExpanded = true;
        }

        /// <summary>
        /// Gets the keys of every node, in display order.
        /// </summary>
        /// <returns>The keys.</returns>
        public GuiCollection GetAllNodeKeys()
        {
            return new GuiCollection(GetNodes(Tree.Items).Select(i => i.Tag));
        }

        /// <summary>
        /// Gets the text of a node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <returns>The node text.</returns>
        public string GetNodeTextByKey(string nodeKey)
        {
            return GetNode(nodeKey).Header as string ?? string.Empty;
        }

        /// <summary>
        /// Selects a node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        public void SelectNode(string nodeKey)
        {
            // Select the node; an unknown key raises an error.
            GetNode(nodeKey).IsSelected = true;
        }

        // Lists the tree items depth-first, in display order.
        private static IEnumerable<TreeViewItem> GetNodes(ItemCollection items)
        {
            // Yield each item before its children, which is the display order.
            foreach (var item in items.OfType<TreeViewItem>())
            {
                yield return item;

                foreach (var child in GetNodes(item.Items))
                {
                    yield return child;
                }
            }
        }

        // Finds a node by key, raising an error that names the key when none matches.
        private TreeViewItem GetNode(string nodeKey)
        {
            // Search every level; keys are unique within the tree.
            return GetNodes(Tree.Items).FirstOrDefault(i => string.Equals(i.Tag as string, nodeKey, StringComparison.Ordinal))
                ?? throw new SimulatorException($"The tree '{Id}' has no node with key '{nodeKey}'.");
        }
        #endregion
    }
}
