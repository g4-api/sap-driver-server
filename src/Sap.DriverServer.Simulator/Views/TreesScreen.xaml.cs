using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the tree test lab (transaction <c>ZSIM_TREES</c>): the same flight hierarchy as a simple, a list, and a
    /// column tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The trees are <c>usr/cntlTREE_SIMPLE/shellcont/shell</c>, <c>usr/cntlTREE_LIST/shellcont/shell</c>, and
    /// <c>usr/cntlTREE_COLUMN/shellcont/shell</c>. The traps they show:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Node keys are right-aligned numbers padded with blanks to ten characters, such as <c>"         1"</c>.</description></item>
    /// <item><description>Folders load their children on the first expansion; a child key is unknown before that.</description></item>
    /// <item><description>List and column trees hold their texts in items, read with <c>GetItemText</c>; list tree items are named <c>1</c> to <c>3</c>, column tree items by column.</description></item>
    /// </list>
    /// <para>
    /// Double-clicking a node or an item reports it in the status bar. The data is synthetic and fixed.
    /// </para>
    /// </remarks>
    public partial class TreesScreen : UserControl
    {
        #region *** Constants    ***
        // Lists the airlines of the hierarchy with their names.
        private static readonly (string Code, string Name)[] Airlines = [("SIM", "Simulator Air"), ("TST", "Test Airways"), ("DMO", "Demo Jet")];

        // Lists the column names of the column tree; the first is the hierarchy column.
        private static readonly string[] ColumnNames = ["HIER", "PRICE", "SEATS"];

        // Lists the column titles of the column tree, in column order.
        private static readonly string[] ColumnTitles = ["Flight", "Price", "Free seats"];

        // Sets the width that node keys are padded to, as SAP's numeric node keys are.
        private const int KeyWidth = 10;

        // Lists the item names of the list tree.
        private static readonly string[] ListItemNames = ["1", "2", "3"];
        #endregion

        #region *** Fields       ***
        // Reports double-clicked nodes and items.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="TreesScreen"/> class.
        /// </summary>
        /// <param name="session">The session that reports double clicks.</param>
        public TreesScreen(SimulatedSession session)
        {
            // Require the session that reports double clicks, then build the screen.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();

            // Show the same hierarchy in all three tree types; each tree gets its own node models.
            SimpleTreeView.TreeType = TreeHost.SimpleTree;
            SimpleTreeView.ShowNodes([NewHierarchy(itemNames: [])]);
            ListTreeView.ItemNames = ListItemNames;
            ListTreeView.TreeType = TreeHost.ListTree;
            ListTreeView.ShowNodes([NewHierarchy(ListItemNames)]);
            ColumnTreeView.ColumnTitles = ColumnTitles;
            ColumnTreeView.ItemNames = ColumnNames;
            ColumnTreeView.TreeType = TreeHost.ColumnTree;
            ColumnTreeView.ShowNodes([NewHierarchy(ColumnNames)]);

            // Draw the column tree's titles above its items, with the item widths the tree uses.
            for (var index = 0; index < ColumnTitles.Length; index++)
            {
                var title = new TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 12, 0),
                    Text = ColumnTitles[index],
                    Width = index == 0 ? 180 : 100
                };
                ColumnHeaders.Children.Add(title);
            }
        }
        #endregion

        #region *** Methods      ***
        // Builds the flight hierarchy: a root folder, one folder per airline, and three flights per airline.
        private static TreeHost.TreeNode NewHierarchy(IReadOnlyList<string> itemNames)
        {
            // Number the nodes depth-first by level: root 1, airlines 2 to 4, flights 5 to 13; nothing is random.
            var airlines = new List<TreeHost.TreeNode>();
            var flightNumber = Airlines.Length + 2;

            for (var index = 0; index < Airlines.Length; index++)
            {
                var (code, name) = Airlines[index];
                var flights = new List<TreeHost.TreeNode>();

                for (var flight = 0; flight < 3; flight++)
                {
                    var connection = string.Create(CultureInfo.InvariantCulture, $"{17 + (((index * 3) + flight) * 4):0000}");
                    var price = string.Create(CultureInfo.InvariantCulture, $"{150 + (((index * 3) + flight) * 25)}.00");
                    var seats = string.Create(CultureInfo.InvariantCulture, $"{10 + (((index * 3) + flight) * 7)}");
                    var text = $"{code} {connection}";
                    flights.Add(NewNode(flightNumber++, text, itemNames, [text, price, seats], children: []));
                }

                airlines.Add(NewNode(index + 2, $"{code} {name}", itemNames, [$"{code} {name}", string.Empty, string.Empty], flights));
            }

            return NewNode(1, "Flights", itemNames, ["Flights", string.Empty, string.Empty], airlines);
        }

        // Creates a node; the three values fill the items under the given item names, in order.
        private static TreeHost.TreeNode NewNode(int number, string text, IReadOnlyList<string> itemNames, string[] values, IReadOnlyList<TreeHost.TreeNode> children)
        {
            // Pad the key like SAP's numeric node keys and pair each item name with its value.
            return new TreeHost.TreeNode
            {
                Children = children,
                Items = itemNames.Zip(values).ToDictionary(i => i.First, i => i.Second),
                Key = number.ToString(CultureInfo.InvariantCulture).PadLeft(KeyWidth),
                Text = text
            };
        }

        // Reports the double-clicked node, and the item when a script double-clicked one.
        private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Read the node from the selection; double clicks outside a node change nothing.
            var tree = (TreeHost)sender;
            if ((tree.SelectedItem as TreeViewItem)?.Tag is not string key)
            {
                return;
            }

            // Name the item when there is one, so tests can tell node and item double clicks apart.
            e.Handled = true;
            var text = tree.SelectedItemName.Length == 0
                ? $"Node '{key}' double-clicked."
                : $"Item '{tree.SelectedItemName}' of node '{key}' double-clicked.";
            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "080", text));
        }
        #endregion
    }
}
