using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows SAP Easy Access: a menu tree whose leaves start the simulator's transactions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree is built from the screen catalog: the root folder <c>N1</c> holds the demo transactions, followed by
    /// the test lab folder and its <c>ZSIM_*</c> transactions. Node keys are numbered in that order (<c>N2</c>,
    /// <c>N3</c>, and so on), so they stay stable while later test lab transactions are appended.
    /// </para>
    /// <para>
    /// Double-clicking a leaf, by mouse or through the tree shell's <c>doubleClickNode</c>, starts its transaction.
    /// </para>
    /// </remarks>
    public partial class EasyAccessScreen : UserControl
    {
        #region *** Fields       ***
        // Starts the transaction of a double-clicked leaf.
        private readonly SimulatedSession _session;

        // Maps each leaf's node key to the transaction it starts; folders have no entry.
        private readonly Dictionary<string, string> _transactions = new(StringComparer.Ordinal);
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="EasyAccessScreen"/> class.
        /// </summary>
        /// <param name="session">The session that runs the selected transaction.</param>
        public EasyAccessScreen(SimulatedSession session)
        {
            // Require the session that starts the transactions, then build the tree.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();

            // Number the nodes in display order: the root, the demos, the test lab folder, then its transactions.
            var root = NewNode(1, "SAP GUI Simulator");
            AddLeaves(root, ScreenCatalog.GetDemoTransactions());
            var testLab = NewNode(root.Items.Count + 2, "Test lab");
            root.Items.Add(testLab);
            AddLeaves(testLab, ScreenCatalog.GetTestLabTransactions());
            MenuTree.Items.Add(root);
        }
        #endregion

        #region *** Methods      ***
        // Creates an expanded node with the key N<number>.
        private static TreeViewItem NewNode(int number, string text)
        {
            return new TreeViewItem
            {
                Header = text,
                IsExpanded = true,
                Tag = string.Create(CultureInfo.InvariantCulture, $"N{number}")
            };
        }

        // Adds one leaf per transaction, continuing the key numbering after the folder's own key.
        private void AddLeaves(TreeViewItem folder, IReadOnlyList<string> transactions)
        {
            // Continue after the folder key, so every key in the tree is unique and follows display order.
            var number = int.Parse(((string)folder.Tag)[1..], CultureInfo.InvariantCulture);

            foreach (var transaction in transactions)
            {
                var title = ScreenCatalog.GetScreen(transaction).Title;
                var leaf = NewNode(++number, $"{title} ({transaction})");
                _transactions[(string)leaf.Tag] = transaction;
                folder.Items.Add(leaf);
            }
        }

        // Starts the transaction of the selected leaf; folders only toggle.
        private void OnMenuTreeDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Map the selected leaf to its transaction; folders map to nothing.
            if ((MenuTree.SelectedItem as TreeViewItem)?.Tag is string key && _transactions.TryGetValue(key, out var transaction))
            {
                e.Handled = true;
                _session.SubmitCommand(transaction);
            }
        }
        #endregion
    }
}
