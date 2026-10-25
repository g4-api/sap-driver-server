using System;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the form screen (transaction <c>FORM</c>): one control of every common dynpro type and a popup button.
    /// </summary>
    public partial class FormScreen : UserControl
    {
        #region *** Fields       ***
        // Opens the popup when the button is pressed.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="FormScreen"/> class.
        /// </summary>
        /// <param name="session">The session that owns the screen.</param>
        public FormScreen(SimulatedSession session)
        {
            // Require the session that opens the popup, then build the form.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();
        }
        #endregion

        #region *** Methods      ***
        // Opens the confirmation popup over the main window.
        private void OnOpenPopupClick(object sender, RoutedEventArgs e)
        {
            // Open wnd[1]; the session refuses a second popup.
            _session.OpenPopup();
        }
        #endregion
    }
}
