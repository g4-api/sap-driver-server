using System;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows a popup (<c>wnd[1]</c>): a confirmation, a message, or an input help, with an optional toolbar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The popup is shown modeless and the session blocks <c>wnd[0]</c> while it is open, so a scripting call that
    /// opens it returns at once instead of waiting in a modal message loop.
    /// </para>
    /// <para>
    /// The window owns the toolbar (<c>tbar[0]</c>) and the keyboard: the confirm button (<c>btn[0]</c>) and Enter
    /// run the content's default action, and the cancel button (<c>btn[12]</c>), F12, and Escape close a cancelable
    /// popup without an answer. Both routes go through <see cref="SimulatedSession.SendVirtualKey"/>, so manual and
    /// scripted input behave the same.
    /// </para>
    /// </remarks>
    public partial class ModalFrameWindow : FrameWindow
    {
        #region *** Constants    ***
        // Appends the test-double marker to every Windows caption, so nobody mistakes the window for SAP GUI.
        private const string CaptionSuffix = " - SAP GUI Simulator (test double)";
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ModalFrameWindow"/> class.
        /// </summary>
        /// <param name="session">The session that owns the popup.</param>
        /// <param name="screen">The popup content; it must be a WPF element.</param>
        public ModalFrameWindow(SimulatedSession session, IPopupScreen screen)
            : base(session)
        {
            // Require content the window can show, then place it and configure the toolbar from it.
            ArgumentNullException.ThrowIfNull(argument: screen);

            InitializeComponent();
            Screen = screen;
            PopupContent.Content = screen;
            Title = screen.Title + CaptionSuffix;

            // Hide the toolbar for popups that answer through their own buttons; hidden controls are not scriptable.
            Toolbar.Visibility = screen.ConfirmText is null ? Visibility.Collapsed : Visibility.Visible;
            ConfirmButton.Content = screen.ConfirmText;
            CancelButton.Visibility = screen.IsCancelable ? Visibility.Visible : Visibility.Collapsed;
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public override FrameworkElement ContentRoot => RootGrid;

        /// <summary>
        /// Gets the popup content.
        /// </summary>
        public IPopupScreen Screen { get; }

        /// <inheritdoc />
        public override string ScreenTitle => Screen.Title;

        /// <inheritdoc />
        public override int WindowIndex => 1;

        /// <inheritdoc />
        protected override Canvas HighlightLayer => Highlights;
        #endregion

        #region *** Methods      ***
        // Closes the popup without an answer, as F12 does.
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            // Send Cancel through the session, which refuses it for popups that cannot be cancelled.
            SendVirtualKey(SimulatedSession.CancelKey);
        }

        // Runs the popup's default action, as Enter does.
        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            // Send Enter through the session, which runs the content's default action.
            SendVirtualKey(SimulatedSession.EnterKey);
        }

        // Turns Enter, Escape, and F12 into the popup's virtual keys.
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Map the popup's keys; every other key reaches the focused control unchanged.
            var key = e.Key switch
            {
                Key.Enter => SimulatedSession.EnterKey,
                Key.Escape or Key.F12 => SimulatedSession.CancelKey,
                _ => -1
            };

            if (key < 0)
            {
                return;
            }

            // Consume the key so the focused control does not also process it.
            e.Handled = true;
            SendVirtualKey(key);
        }

        // Sends a virtual key from manual input, beeping when the session refuses it.
        private void SendVirtualKey(int key)
        {
            // Let the session run the key, so manual and scripted input share one path.
            try
            {
                Session.SendVirtualKey(WindowIndex, key);
            }
            catch (SimulatorException)
            {
                // Signal a refused key the way a desktop application does, without interrupting the user.
                SystemSounds.Beep.Play();
            }
        }
        #endregion
    }
}
