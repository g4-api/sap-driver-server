using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows a SAP tree control: a simple tree, a list tree, or a column tree over a fixed node model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SAP's tree control has three types, which <c>GetTreeType</c> reports: a simple tree (0) shows one text per
    /// node; a list tree (1) shows several items per node, named <c>1</c>, <c>2</c>, and so on; and a column tree (2)
    /// shows one item per column under column headers, named by column. Item texts are read with <c>GetItemText</c>.
    /// </para>
    /// <para>
    /// The node model is complete and its keys are fixed, so keys do not depend on the order in which folders open.
    /// The tree items, though, are created only when their parent folder expands, as SAP loads the children of a
    /// folder on its first expansion: a child's key is unknown to scripting until the folder was expanded once.
    /// </para>
    /// </remarks>
    public sealed class TreeHost : TreeView
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the column tree type.
        /// </summary>
        public const int ColumnTree = 2;

        /// <summary>
        /// Specifies the list tree type.
        /// </summary>
        public const int ListTree = 1;

        /// <summary>
        /// Specifies the simple tree type.
        /// </summary>
        public const int SimpleTree = 0;

        // Marks the placeholder child that gives a folder its expander before its children are loaded.
        private const string PlaceholderTag = "(not loaded)";
        #endregion

        #region *** Fields       ***
        // Maps every loaded node key to its model, for text and item lookups.
        private readonly Dictionary<string, TreeNode> _loaded = new(StringComparer.Ordinal);
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the column titles of a column tree, in item name order; empty for the other types.
        /// </summary>
        public IReadOnlyList<string> ColumnTitles { get; set; } = [];

        /// <summary>
        /// Gets or sets the item names in display order: column names for a column tree, <c>1</c>, <c>2</c>, ... for a
        /// list tree, and empty for a simple tree.
        /// </summary>
        public IReadOnlyList<string> ItemNames { get; set; } = [];

        /// <summary>
        /// Gets or sets the name of the item a script selected or double-clicked last; empty when none was.
        /// </summary>
        public string SelectedItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tree type: <see cref="SimpleTree"/>, <see cref="ListTree"/>, or <see cref="ColumnTree"/>.
        /// </summary>
        public int TreeType { get; set; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the model of a loaded node.
        /// </summary>
        /// <param name="nodeKey">The node key, including any padding.</param>
        /// <returns>The node; null when no loaded node has the key.</returns>
        public TreeNode GetLoadedNode(string nodeKey)
        {
            return nodeKey is not null && _loaded.TryGetValue(nodeKey, out var node) ? node : null;
        }

        /// <summary>
        /// Shows the root nodes; their children load when they expand.
        /// </summary>
        /// <param name="roots">The root nodes in display order.</param>
        public void ShowNodes(IEnumerable<TreeNode> roots)
        {
            // Start empty, then add each root with its expander, but without its children.
            ArgumentNullException.ThrowIfNull(argument: roots);

            Items.Clear();
            _loaded.Clear();
            foreach (var root in roots)
            {
                Items.Add(NewItem(root));
            }
        }

        /// <summary>
        /// Tests whether a tree item is a loaded node rather than the placeholder of an unloaded folder.
        /// </summary>
        /// <param name="item">The tree item.</param>
        /// <returns>True when the item stands for a node.</returns>
        public bool TestNodeItem(TreeViewItem item)
        {
            return item?.Tag is string key && _loaded.ContainsKey(key);
        }

        // Adds a folder's children in place of its placeholder, once, on the first expansion.
        private void LoadChildren(TreeViewItem item, TreeNode node)
        {
            // Load only once; later expansions show the same items.
            if (item.Items.Count != 1 || (item.Items[0] as TreeViewItem)?.Tag as string != PlaceholderTag)
            {
                return;
            }

            item.Items.Clear();
            foreach (var child in node.Children)
            {
                item.Items.Add(NewItem(child));
            }
        }

        // Creates the header of a node: its text for a simple tree, one cell per item for list and column trees.
        private object NewHeader(TreeNode node)
        {
            // Simple trees show plain text, which also keeps the header readable as a string.
            if (TreeType == SimpleTree)
            {
                return node.Text;
            }

            // Show the items side by side; column trees use fixed widths, so the cells line up under the titles.
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var name in ItemNames)
            {
                var text = node.Items.TryGetValue(name, out var value) ? value : string.Empty;
                var cell = new TextBlock { Margin = new Thickness(0, 0, 12, 0), Text = text };
                if (TreeType == ColumnTree)
                {
                    cell.Width = name == ItemNames[0] ? 180 : 100;
                }

                panel.Children.Add(cell);
            }

            return panel;
        }

        // Creates the tree item of a node; folders get a placeholder child, so they show an expander.
        private TreeViewItem NewItem(TreeNode node)
        {
            // Register the node for lookups and create its item with the key in the tag.
            _loaded[node.Key] = node;
            var item = new TreeViewItem { Header = NewHeader(node), Tag = node.Key };

            // Give folders a placeholder until they expand; the placeholder is not a node.
            if (node.Children.Count > 0)
            {
                item.Items.Add(new TreeViewItem { Foreground = Brushes.Gray, Header = "...", Tag = PlaceholderTag });
                item.Expanded += (_, e) =>
                {
                    // Handle the event once, at the item that expanded, not at its ancestors.
                    if (ReferenceEquals(e.OriginalSource, item))
                    {
                        LoadChildren(item, node);
                    }
                };
            }

            return item;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents one tree node: its key, its text, its items, and its children.
        /// </summary>
        public sealed class TreeNode
        {
            #region *** Properties   ***
            /// <summary>
            /// Gets the child nodes; empty for a leaf.
            /// </summary>
            public IReadOnlyList<TreeNode> Children { get; init; } = [];

            /// <summary>
            /// Gets the item texts by item name, for list and column trees.
            /// </summary>
            public IReadOnlyDictionary<string, string> Items { get; init; } = new Dictionary<string, string>();

            /// <summary>
            /// Gets the node key, such as <c>"         1"</c>; SAP keys keep their padding.
            /// </summary>
            public string Key { get; init; }

            /// <summary>
            /// Gets the node text, which a simple tree shows.
            /// </summary>
            public string Text { get; init; }
            #endregion
        }
        #endregion
    }
}
