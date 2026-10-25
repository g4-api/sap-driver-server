using System;
using System.Windows.Controls;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the grid screen (transaction <c>GRID</c>): synthetic flights in a grid view shell.
    /// </summary>
    /// <remarks>
    /// Double-clicking a row, by mouse or through the shell's <c>doubleClickCurrentCell</c>, reports the row in the
    /// status bar, which gives tests an observable result for grid interaction.
    /// </remarks>
    public partial class GridScreen : UserControl
    {
        #region *** Fields       ***
        // Reports the double-clicked row in the status bar.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GridScreen"/> class.
        /// </summary>
        /// <param name="session">The session that owns the screen.</param>
        public GridScreen(SimulatedSession session)
        {
            // Require the session that reports double clicks, then fill the grid with the synthetic rows.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();
            FlightGrid.ItemsSource = FlightRow.NewRows();
        }
        #endregion

        #region *** Methods      ***
        // Reports the current row; double-clicks outside a row change nothing.
        private void OnFlightGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Report nothing when the double click did not land on a row.
            if (FlightGrid.CurrentItem is not FlightRow row)
            {
                return;
            }

            var text = $"Flight {row.Airline} {row.FlightNumber} on {row.FlightDate} selected.";
            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "030", text));
        }
        #endregion
    }
}
