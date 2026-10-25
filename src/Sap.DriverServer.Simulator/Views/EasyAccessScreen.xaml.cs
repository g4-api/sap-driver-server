using System;
using System.Windows.Controls;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows SAP Easy Access: a menu tree whose leaves start the simulator's transactions.
    /// </summary>
    /// <remarks>
    /// Double-clicking a leaf, by mouse or through the tree shell's <c>doubleClickNode</c>, starts its transaction.
    /// </remarks>
    public partial class EasyAccessScreen : UserControl
    {
        #region *** Fields       ***
        // Starts the transaction of a double-clicked leaf.
        private readonly SimulatedSession _session;
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
        }
        #endregion

        #region *** Methods      ***
        // Starts the transaction of the selected leaf; folders only toggle.
        private void OnMenuTreeDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Map the selected leaf to its transaction; folders map to nothing.
            var transaction = (MenuTree.SelectedItem as TreeViewItem)?.Tag switch
            {
                "N2" => ScreenCatalog.Form,
                "N3" => ScreenCatalog.Grid,
                _ => null
            };

            if (transaction is not null)
            {
                e.Handled = true;
                _session.SubmitCommand(transaction);
            }
        }
        #endregion
    }
}
