using System;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the detail popup of an ALV row: the row's values in output fields under <c>usr/txtDETAIL-*</c>.
    /// </summary>
    /// <remarks>
    /// Enter or <c>tbar[0]/btn[0]</c> closes the popup; F12 cancels it. Neither changes the grid.
    /// </remarks>
    public partial class RowDetailPopup : UserControl, IPopupScreen
    {
        #region *** Fields       ***
        // Closes the popup when the user continues.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="RowDetailPopup"/> class.
        /// </summary>
        /// <param name="session">The session that closes the popup.</param>
        /// <param name="row">The row whose values are shown.</param>
        public RowDetailPopup(SimulatedSession session, FlightRow row)
        {
            // Assert the constructor contract before any content is built.
            AssertArguments(session, row);

            // Build the content and fill the output fields from the row.
            _session = session;
            InitializeComponent();
            AirlineField.Text = row.Airline;
            FlightNumberField.Text = row.FlightNumber;
            DateField.Text = row.FlightDate;
            PriceField.Text = row.Price;

            return;

            // Asserts the session and the row before the popup is built; nothing is mutated, and a missing argument
            // fails the construction with an ArgumentNullException.
            static void AssertArguments(SimulatedSession session, FlightRow row)
            {
                // Require the session that closes the popup and the row it shows.
                ArgumentNullException.ThrowIfNull(argument: session);
                ArgumentNullException.ThrowIfNull(argument: row);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public string ConfirmText => "Continue";

        /// <inheritdoc />
        public bool IsCancelable => true;

        /// <inheritdoc />
        public string Title => "Flight Details";
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Confirm()
        {
            // Close the popup without changing the status bar.
            _session.CompletePopup(message: null);
        }
        #endregion
    }
}
