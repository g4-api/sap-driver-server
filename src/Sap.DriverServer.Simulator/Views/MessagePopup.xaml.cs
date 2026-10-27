using System;
using System.Windows.Controls;
using System.Windows.Media;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows a message in a dialog box: information and termination messages, and success, warning, or error
    /// messages when they are shown as popups.
    /// </summary>
    /// <remarks>
    /// The text is in <c>usr/txtMESSTXT1</c>, and the toolbar's <c>btn[0]</c> (Continue) or Enter closes the popup. A
    /// message dialog box cannot be cancelled. The titles are the simulator's choice by message type.
    /// </remarks>
    public partial class MessagePopup : UserControl, IPopupScreen
    {
        #region *** Fields       ***
        // Closes the popup when the user continues.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="MessagePopup"/> class.
        /// </summary>
        /// <param name="session">The session that closes the popup.</param>
        /// <param name="message">The message to show.</param>
        public MessagePopup(SimulatedSession session, StatusMessage message)
        {
            // Assert the constructor contract before any content is built.
            AssertArguments(session, message);

            // Build the content, then show the text with a title and a mark for the message type.
            _session = session;
            InitializeComponent();
            Title = message.MessageType switch
            {
                StatusMessage.Abort => "Termination",
                StatusMessage.Error => "Error",
                StatusMessage.Warning => "Warning",
                _ => "Information"
            };
            MessageText.Text = message.Text;
            TypeMark.Text = message.MessageType;
            TypeMark.Foreground = message.MessageType is StatusMessage.Error or StatusMessage.Abort ? Brushes.DarkRed : Brushes.DarkSlateBlue;

            return;

            // Asserts the session and the message before the popup is built; nothing is mutated, and a missing
            // argument fails the construction with an ArgumentNullException.
            static void AssertArguments(SimulatedSession session, StatusMessage message)
            {
                // Require the session that closes the popup and the message it shows.
                ArgumentNullException.ThrowIfNull(argument: session);
                ArgumentNullException.ThrowIfNull(argument: message);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public string ConfirmText => "Continue";

        /// <inheritdoc />
        public bool IsCancelable => false;

        /// <inheritdoc />
        public string Title { get; }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Confirm()
        {
            // Close the popup; the status bar keeps the message, as SAP does.
            _session.CompletePopup(message: null);
        }
        #endregion
    }
}
