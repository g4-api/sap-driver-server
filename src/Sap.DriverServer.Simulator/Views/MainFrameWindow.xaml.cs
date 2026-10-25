using System;
using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows a session's main window (<c>wnd[0]</c>): menu bar, system toolbar with the command field, title bar,
    /// application toolbar, user area, and status bar.
    /// </summary>
    /// <remarks>
    /// The window forwards keyboard shortcuts and toolbar buttons to <see cref="SimulatedSession.SendVirtualKey"/>, so
    /// manual input and scripted <c>sendVKey</c> calls run the same code. Actions the session refuses, such as input
    /// while the popup is open, produce a beep instead of an error dialog.
    /// </remarks>
    public partial class MainFrameWindow : FrameWindow
    {
        #region *** Constants    ***
        // Appends the test-double marker to every Windows caption, so nobody mistakes the window for SAP GUI.
        private const string CaptionSuffix = " - SAP GUI Simulator (test double)";
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="MainFrameWindow"/> class.
        /// </summary>
        /// <param name="session">The session that owns the window.</param>
        public MainFrameWindow(SimulatedSession session)
            : base(session)
        {
            InitializeComponent();
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public override FrameworkElement ContentRoot => RootGrid;

        /// <inheritdoc />
        public override string ScreenTitle => TitleText.Text;

        /// <summary>
        /// Gets the content of the user area (<c>usr</c>): the current screen.
        /// </summary>
        public object UserAreaContent => UserArea.Content;

        /// <inheritdoc />
        public override int WindowIndex => 0;

        /// <inheritdoc />
        protected override Canvas HighlightLayer => Highlights;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Shows a screen in the user area and sets the titles.
        /// </summary>
        /// <param name="content">The screen content.</param>
        /// <param name="title">The SAP window title.</param>
        public void ShowScreen(object content, string title)
        {
            // Remove frames that belong to the previous screen, then swap the content and titles.
            ClearHighlights();
            UserArea.Content = content;
            TitleText.Text = title;
            Title = title + CaptionSuffix;
        }

        /// <summary>
        /// Shows a status bar message, colored by its type.
        /// </summary>
        /// <param name="message">The message.</param>
        public void ShowStatus(StatusMessage message)
        {
            // Show the text with SAP's color convention: green success, amber warning, red error.
            ArgumentNullException.ThrowIfNull(argument: message);

            StatusText.Text = message.Text;
            StatusText.Foreground = message.MessageType switch
            {
                StatusMessage.Error => Brushes.DarkRed,
                StatusMessage.Success => Brushes.DarkGreen,
                StatusMessage.Warning => Brushes.DarkGoldenrod,
                _ => Brushes.Black
            };
        }

        /// <summary>
        /// Reads and clears the command field, as SAP GUI does when it processes the OK code.
        /// </summary>
        /// <returns>The trimmed OK code; empty when the field is empty.</returns>
        public string TakeCommand()
        {
            // Read the code and empty the field, so the same code does not run twice.
            var command = CommandField.Text.Trim();
            CommandField.Clear();

            return command;
        }

        // Maps a keyboard shortcut to its SAP virtual key; returns -1 for keys without one.
        private static int ConvertToVirtualKey(KeyEventArgs e)
        {
            // Read the real key behind system keys, such as F10, and the modifiers held with it.
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            return key switch
            {
                Key.Enter => SimulatedSession.EnterKey,
                Key.F3 when modifiers == ModifierKeys.Shift => SimulatedSession.ExitKey,
                Key.F3 => SimulatedSession.BackKey,
                Key.F12 => SimulatedSession.CancelKey,
                Key.S when modifiers == ModifierKeys.Control => SimulatedSession.SaveKey,
                _ => -1
            };
        }

        // Opens SAP Easy Access from the menu, like the /n command.
        private void OnEasyAccessMenuClick(object sender, RoutedEventArgs e)
        {
            // Run the empty OK code, which returns to SAP Easy Access after logon.
            Session.SubmitCommand(command: null);
        }

        // Logs off from the menu.
        private void OnLogOffMenuClick(object sender, RoutedEventArgs e)
        {
            // Log off, which also closes an open popup.
            Session.LogOff();
        }

        // Turns keyboard shortcuts into virtual keys; other keys reach the focused control.
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = ConvertToVirtualKey(e);
            if (key < 0)
            {
                return;
            }

            // Consume the key so the focused control does not also process it.
            e.Handled = true;
            SendVirtualKey(key);
        }

        // Sends the virtual key stored in the pressed toolbar button's tag.
        private void OnToolbarButtonClick(object sender, RoutedEventArgs e)
        {
            // Read the button's virtual key from its tag and send it like a keyboard shortcut.
            var tag = Convert.ToString(((FrameworkElement)sender).Tag, CultureInfo.InvariantCulture);
            SendVirtualKey(int.Parse(tag, CultureInfo.InvariantCulture));
        }

        // Sends a virtual key from manual input, beeping when the session refuses it.
        private void SendVirtualKey(int key)
        {
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
