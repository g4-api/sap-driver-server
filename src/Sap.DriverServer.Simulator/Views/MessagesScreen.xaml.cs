using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the messages and popups test lab (transaction <c>ZSIM_MESSAGES</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each message button raises one message type with a fixed text and number:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Success (<c>S</c>), warning (<c>W</c>), and error (<c>E</c>) messages appear in the status bar, or in a popup when <c>chkMSG_AS_POPUP</c> is selected.</description></item>
    /// <item><description>Information messages (<c>I</c>) always appear in a popup.</description></item>
    /// <item><description>Termination messages (<c>A</c>) appear in a popup and end the transaction when it closes.</description></item>
    /// </list>
    /// <para>
    /// A message shown in a popup is also the status bar's message, with <c>MessageAsPopup</c> set. The behavior
    /// follows the SAP GUI Scripting documentation; it has not been compared with a live SAP system.
    /// </para>
    /// </remarks>
    public partial class MessagesScreen : UserControl
    {
        #region *** Fields       ***
        // Raises the messages and opens the popups.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="MessagesScreen"/> class.
        /// </summary>
        /// <param name="session">The session that shows the messages.</param>
        public MessagesScreen(SimulatedSession session)
        {
            // Require the session that shows the messages, then build the screen.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();
        }
        #endregion

        #region *** Methods      ***
        // Creates the fixed message of a type; the numbers 040 to 044 belong to this screen.
        private static StatusMessage NewMessage(string messageType)
        {
            // Pick the number and text by type; every text says it is simulated.
            var (number, text) = messageType switch
            {
                StatusMessage.Abort => ("044", "Transaction terminated (simulated)."),
                StatusMessage.Error => ("042", "The quantity exceeds the available stock (simulated)."),
                StatusMessage.Information => ("043", "The document will be posted in the next period (simulated)."),
                StatusMessage.Warning => ("041", "The delivery date is in the past (simulated)."),
                _ => ("040", "Document 4711 posted (simulated).")
            };

            return StatusMessage.NewMessage(messageType, number, text);
        }

        // Opens the confirmation popup over the main window.
        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            // Open wnd[1]; the session refuses a second popup.
            _session.OpenPopup(new ConfirmPopup(_session));
        }

        // Raises the message type stored in the pressed button's tag.
        private void OnMessageClick(object sender, RoutedEventArgs e)
        {
            // Read the type from the tag and let the session decide between the status bar and a popup.
            var messageType = Convert.ToString(((FrameworkElement)sender).Tag, CultureInfo.InvariantCulture);
            _session.ShowMessage(NewMessage(messageType), AsPopupCheckBox.IsChecked == true);
        }
        #endregion
    }
}
