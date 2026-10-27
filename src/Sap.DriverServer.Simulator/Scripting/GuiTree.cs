using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;
using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's tree shell (<c>GuiShell</c> with <c>SubType</c> <c>Tree</c>), such as the SAP Easy Access menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nodes are addressed by key; each tree item's tag holds its key. Keys are compared exactly, padding included, so
    /// <c>"1"</c> does not find the node <c>"         1"</c>. Unknown keys raise an error.
    /// </para>
    /// <para>
    /// Trees built on <see cref="TreeHost"/> add SAP's tree types, list and column items, and lazy folders: a folder's
    /// children are unknown to every member until the folder was expanded once. Other trees, such as the SAP Easy Access
    /// menu, are simple trees whose nodes all exist from the start.
    /// </para>
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
        /// Gets the name of the item selected last by <c>selectItem</c> or <c>doubleClickItem</c>; empty when none was.
        /// </summary>
        public string SelectedItemColumnName => Host?.SelectedItemName ?? string.Empty;

        /// <summary>
        /// Gets the key of the node whose item was selected last; empty when no item was selected.
        /// </summary>
        public string SelectedItemNode => SelectedItemColumnName.Length == 0 ? string.Empty : SelectedNode;

        /// <summary>
        /// Gets the key of the selected node; empty when no node is selected.
        /// </summary>
        public string SelectedNode => (Tree.SelectedItem as TreeViewItem)?.Tag as string ?? string.Empty;

        /// <inheritdoc />
        public override string SubType => "Tree";

        // Gets the wrapped tree as a SAP tree control; null for plain trees such as the SAP Easy Access menu.
        private TreeHost Host => Element as TreeHost;

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
        /// Selects an item of a node and double-clicks it.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <param name="itemName">The item name: a column name in a column tree, <c>1</c>, <c>2</c>, ... in a list tree.</param>
        public void DoubleClickItem(string nodeKey, string itemName)
        {
            // Select the item first, because the double-click handler reads the selected node and item.
            SelectItem(nodeKey, itemName);
            RaiseDoubleClick();
        }

        /// <summary>
        /// Selects a node and double-clicks it, which starts the node's transaction in SAP Easy Access.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        public void DoubleClickNode(string nodeKey)
        {
            // Select the node first, because the double-click handler reads the selection; no item is involved.
            SelectNode(nodeKey);
            RaiseDoubleClick();
        }

        /// <summary>
        /// Expands a node; a lazy folder loads its children on the first expansion.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        public void ExpandNode(string nodeKey)
        {
            // Expand the node; an unknown key raises an error.
            GetNode(nodeKey).IsExpanded = true;
        }

        /// <summary>
        /// Gets the keys of every loaded node, depth first in display order.
        /// </summary>
        /// <returns>The node keys; children of folders that never expanded are not included.</returns>
        public GuiCollection GetAllNodeKeys()
        {
            return new GuiCollection(GetNodes(Tree.Items).Select(i => i.Tag));
        }

        /// <summary>
        /// Gets the column names of a column tree, in display order.
        /// </summary>
        /// <returns>The column names; empty for simple and list trees.</returns>
        public GuiCollection GetColumnNames()
        {
            return new GuiCollection(Host?.TreeType == TreeHost.ColumnTree ? Host.ItemNames : []);
        }

        /// <summary>
        /// Gets the column titles of a column tree, in display order.
        /// </summary>
        /// <returns>The column titles; empty for simple and list trees.</returns>
        public GuiCollection GetColumnTitles()
        {
            return new GuiCollection(Host?.TreeType == TreeHost.ColumnTree ? Host.ColumnTitles : []);
        }

        /// <summary>
        /// Gets the text of one item of a node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <param name="itemName">The item name: a column name in a column tree, <c>1</c>, <c>2</c>, ... in a list tree.</param>
        /// <returns>The item text.</returns>
        /// <exception cref="SimulatorException">The node or the item does not exist.</exception>
        public string GetItemText(string nodeKey, string itemName)
        {
            // Read the item from the node model; simple trees and unknown items have none.
            var node = GetNodeModel(nodeKey);
            if (node?.Items.TryGetValue(itemName ?? string.Empty, out var text) == true)
            {
                return text;
            }

            throw new SimulatorException($"The node '{nodeKey}' of the tree '{Id}' has no item '{itemName}'.");
        }

        /// <summary>
        /// Gets the number of loaded children of a node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <returns>The number of children; zero for a leaf or a folder that never expanded.</returns>
        public int GetNodeChildrenCount(string nodeKey)
        {
            return GetChildItems(GetNode(nodeKey).Items).Count();
        }

        /// <summary>
        /// Gets the text of a node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <returns>The node text.</returns>
        public string GetNodeTextByKey(string nodeKey)
        {
            // Prefer the model of a SAP tree control, whose headers are not plain text in list and column trees.
            var item = GetNode(nodeKey);

            return GetNodeModel(nodeKey)?.Text ?? item.Header as string ?? string.Empty;
        }

        /// <summary>
        /// Gets the key of a node's parent.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <returns>The parent key; empty for a root node.</returns>
        public string GetParent(string nodeKey)
        {
            return (ItemsControl.ItemsControlFromItemContainer(GetNode(nodeKey)) as TreeViewItem)?.Tag as string ?? string.Empty;
        }

        /// <summary>
        /// Gets the keys of a node's loaded children.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <returns>The child keys in display order; empty for a leaf or a folder that never expanded.</returns>
        public GuiCollection GetSubNodesCol(string nodeKey)
        {
            return new GuiCollection(GetChildItems(GetNode(nodeKey).Items).Select(i => i.Tag));
        }

        /// <summary>
        /// Gets the tree type: 0 for a simple tree, 1 for a list tree, and 2 for a column tree.
        /// </summary>
        /// <returns>The tree type.</returns>
        public int GetTreeType()
        {
            return Host?.TreeType ?? TreeHost.SimpleTree;
        }

        /// <summary>
        /// Tests whether a node is a folder, whether or not its children are loaded.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <returns>True when the node has children.</returns>
        public bool IsFolder(string nodeKey)
        {
            // Folders show an expander, which a placeholder child provides before the children load.
            return GetNode(nodeKey).Items.Count > 0;
        }

        /// <summary>
        /// Selects an item of a node, which also selects the node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        /// <param name="itemName">The item name: a column name in a column tree, <c>1</c>, <c>2</c>, ... in a list tree.</param>
        public void SelectItem(string nodeKey, string itemName)
        {
            // Check the item before anything changes, then select the node and remember the item.
            _ = GetItemText(nodeKey, itemName);
            SelectNode(nodeKey);
            Host.SelectedItemName = itemName;
        }

        /// <summary>
        /// Selects a node.
        /// </summary>
        /// <param name="nodeKey">The node key.</param>
        public void SelectNode(string nodeKey)
        {
            // Select the node and forget any item selection; an unknown key raises an error.
            GetNode(nodeKey).IsSelected = true;
            if (Host is not null)
            {
                Host.SelectedItemName = string.Empty;
            }
        }

        // Gets the node items of an item collection, without placeholders.
        private IEnumerable<TreeViewItem> GetChildItems(ItemCollection items)
        {
            // Plain trees have no placeholders; SAP tree controls know which items stand for nodes.
            return items.OfType<TreeViewItem>().Where(i => Host?.TestNodeItem(i) ?? true);
        }

        // Finds a node by key, raising an error that names the key when none matches.
        private TreeViewItem GetNode(string nodeKey)
        {
            // Search every loaded level; keys are unique within the tree and compared with their padding.
            return GetNodes(Tree.Items).FirstOrDefault(i => string.Equals(i.Tag as string, nodeKey, StringComparison.Ordinal))
                ?? throw new SimulatorException($"The tree '{Id}' has no node with key '{nodeKey}'.");
        }

        // Gets the model of a loaded node of a SAP tree control; null for plain trees.
        private TreeHost.TreeNode GetNodeModel(string nodeKey)
        {
            // Require the node to be loaded first, so lazy folders behave the same for every member.
            _ = GetNode(nodeKey);

            return Host?.GetLoadedNode(nodeKey);
        }

        // Lists the tree items depth-first, in display order, skipping the placeholders of unloaded folders.
        private IEnumerable<TreeViewItem> GetNodes(ItemCollection items)
        {
            // Yield each item before its children, which is the display order.
            foreach (var item in GetChildItems(items))
            {
                yield return item;

                foreach (var child in GetNodes(item.Items))
                {
                    yield return child;
                }
            }
        }
        #endregion
    }
}
