using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Represents one simulated SAP session: its main window, an optional popup, the current screen, and the status bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The session is the screen flow's controller. Keyboard input, toolbar buttons, and scripting calls all end up in
    /// <see cref="SendVirtualKey"/>, <see cref="SubmitCommand"/>, or <see cref="Navigate"/>, so manual use and scripted
    /// use behave the same. Like SAP GUI, a popup <c>wnd[1]</c> blocks <c>wnd[0]</c> until it is answered; unlike
    /// SAP GUI, the block is enforced here rather than by a modal message loop, so a scripting call that opens the
    /// popup returns at once.
    /// </para>
    /// <para>
    /// The logon screen accepts any user and password. The password is checked for presence only and cleared right
    /// away; the simulator never stores or reports it. Failures of a requested action are thrown as
    /// <see cref="SimulatorException"/>, which scripting clients receive as a plain COM error with the message.
    /// </para>
    /// </remarks>
    public sealed class SimulatedSession
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the Back virtual key (F3).
        /// </summary>
        public const int BackKey = 3;

        /// <summary>
        /// Specifies the Cancel virtual key (F12).
        /// </summary>
        public const int CancelKey = 12;

        /// <summary>
        /// Specifies the Enter virtual key.
        /// </summary>
        public const int EnterKey = 0;

        /// <summary>
        /// Specifies the Exit virtual key (Shift+F3).
        /// </summary>
        public const int ExitKey = 15;

        /// <summary>
        /// Specifies the first page virtual key (Ctrl+Page Up).
        /// </summary>
        public const int FirstPageKey = 80;

        /// <summary>
        /// Specifies the last page virtual key (Ctrl+Page Down).
        /// </summary>
        public const int LastPageKey = 83;

        /// <summary>
        /// Specifies the next page virtual key (Page Down).
        /// </summary>
        public const int NextPageKey = 82;

        /// <summary>
        /// Specifies the pick virtual key (F2), which selects the line under the cursor in a list.
        /// </summary>
        public const int PickKey = 2;

        /// <summary>
        /// Specifies the previous page virtual key (Page Up).
        /// </summary>
        public const int PreviousPageKey = 81;

        /// <summary>
        /// Specifies the Save virtual key (Ctrl+S, reported as F11).
        /// </summary>
        public const int SaveKey = 11;

        /// <summary>
        /// Specifies the input help virtual key (F4).
        /// </summary>
        public const int ValueHelpKey = 4;

        // Prefixes a transaction code that leaves the current transaction, as in /nFORM.
        private const string NewTransactionPrefix = "/N";
        #endregion

        #region *** Fields       ***
        // Records whether the open popup was answered, so closing it otherwise reports a cancellation.
        private bool _isPopupAnswered;

        // Holds the main window (wnd[0]); null before Open and after the session closed.
        private MainFrameWindow _mainWindow;

        // Holds the popup (wnd[1]) while it is open; otherwise null.
        private ModalFrameWindow _popup;

        // Runs after the open popup closed by any route, such as ending the transaction after a termination message.
        private Action _popupClosed;

        // Owns the session list this session is removed from when it closes.
        private readonly SimulatedSystem _system;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="SimulatedSession"/> class.
        /// </summary>
        /// <param name="system">The simulated system that owns the session.</param>
        /// <param name="index">The zero-based session index, as in <c>ses[n]</c>.</param>
        public SimulatedSession(SimulatedSystem system, int index)
        {
            // Require the owner; the session reports its system and is removed from it on close.
            ArgumentNullException.ThrowIfNull(argument: system);

            _system = system;
            Index = index;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the active window: the popup while it is open, otherwise the main window.
        /// </summary>
        public FrameWindow ActiveWindow => (FrameWindow)_popup ?? _mainWindow;

        /// <summary>
        /// Gets the client, taken from the logon screen when the user logs on.
        /// </summary>
        public string Client { get; private set; } = "001";

        /// <summary>
        /// Gets the identifier, such as <c>/app/con[0]/ses[0]</c>.
        /// </summary>
        public string Id => string.Create(CultureInfo.InvariantCulture, $"{SimulatedSystem.ConnectionId}/ses[{Index}]");

        /// <summary>
        /// Gets the zero-based session index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets a value indicating whether a simulated server round trip is running.
        /// </summary>
        /// <remarks>
        /// Most actions answer at once; a busy period started by <see cref="StartBusyPeriod"/> keeps the value true
        /// for its duration, and virtual keys are refused meanwhile, so clients see SAP's <c>Busy</c> behavior.
        /// </remarks>
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Gets the transaction of the current screen, one of the <see cref="ScreenCatalog"/> constants.
        /// </summary>
        public string Screen { get; private set; } = ScreenCatalog.Logon;

        /// <summary>
        /// Gets the message shown in the main window's status bar.
        /// </summary>
        public StatusMessage Status { get; private set; } = StatusMessage.Empty;

        /// <summary>
        /// Gets the simulated system that owns the session.
        /// </summary>
        public SimulatedSystem System => _system;

        /// <summary>
        /// Gets the open windows in index order: the main window, then the popup when it is open.
        /// </summary>
        public IReadOnlyList<FrameWindow> Windows => _popup is null ? [_mainWindow] : [_mainWindow, _popup];
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Answers the confirmation popup and closes it, reporting the answer in the main window's status bar.
        /// </summary>
        /// <param name="answer">The chosen option text, such as <c>Yes</c>.</param>
        public void AnswerPopup(string answer)
        {
            // Close the popup as answered and report the chosen option.
            CompletePopup(StatusMessage.NewMessage(StatusMessage.Success, "021", $"Popup answered: {answer}"));
        }

        /// <summary>
        /// Closes the popup as answered, optionally reporting a message in the main window's status bar.
        /// </summary>
        /// <param name="message">The message to report; null keeps the current status bar message.</param>
        public void CompletePopup(StatusMessage message)
        {
            // Ignore answers when no popup is open, such as a second click during closing.
            if (_popup is null)
            {
                return;
            }

            // Record the answer first, so the close handler does not report a cancellation.
            _isPopupAnswered = true;
            if (message is not null)
            {
                ShowStatus(message);
            }

            _popup.Close();
        }

        /// <summary>
        /// Logs off: closes the popup and returns the main window to the logon screen.
        /// </summary>
        public void LogOff()
        {
            // Close an open popup without an answer, then show the logon screen.
            _popup?.Close();
            Navigate(ScreenCatalog.Logon);
            ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "005", "You are logged off."));
        }

        /// <summary>
        /// Navigates the main window to a screen and updates the title and session information.
        /// </summary>
        /// <param name="screen">The target screen, one of the <see cref="ScreenCatalog"/> constants.</param>
        public void Navigate(string screen)
        {
            // Build the screen's user area; each screen calls back into this session for its actions.
            object content = screen switch
            {
                ScreenCatalog.Alv => new AlvScreen(session: this),
                ScreenCatalog.Challenge => new ChallengeScreen(session: this),
                ScreenCatalog.EasyAccess => new EasyAccessScreen(session: this),
                ScreenCatalog.Fields => new FieldsScreen(),
                ScreenCatalog.Form => new FormScreen(session: this),
                ScreenCatalog.Grid => new GridScreen(session: this),
                ScreenCatalog.List => new ListScreen(session: this),
                ScreenCatalog.Messages => new MessagesScreen(session: this),
                ScreenCatalog.Shells => new ShellsScreen(session: this),
                ScreenCatalog.Table => new TableScreen(),
                ScreenCatalog.Tabs => new TabsScreen(),
                ScreenCatalog.Trees => new TreesScreen(session: this),
                _ => new LogonScreen()
            };

            // Show it with its title; the screen change also removes every SAP-style highlight.
            Screen = screen;
            _mainWindow.ShowScreen(content, ScreenCatalog.GetScreen(screen).Title);
        }

        /// <summary>
        /// Opens the session's main window on the logon screen.
        /// </summary>
        public void Open()
        {
            // Create the window, place it so several sessions do not cover each other, and show the logon screen.
            _mainWindow = new MainFrameWindow(session: this)
            {
                Left = 80 + (Index * 48),
                Top = 60 + (Index * 48)
            };
            _mainWindow.Closed += (_, _) => OnMainWindowClosed();
            Navigate(ScreenCatalog.Logon);
            _mainWindow.Show();
        }

        /// <summary>
        /// Opens a popup (<c>wnd[1]</c>) over the main window.
        /// </summary>
        /// <param name="screen">The popup content, such as a confirmation or a message.</param>
        /// <exception cref="SimulatorException">A popup is already open.</exception>
        public void OpenPopup(IPopupScreen screen)
        {
            OpenPopup(screen, closed: null);
        }

        /// <summary>
        /// Opens a popup (<c>wnd[1]</c>) over the main window and runs an action after it closed.
        /// </summary>
        /// <param name="screen">The popup content, such as a confirmation or a message.</param>
        /// <param name="closed">The action to run after the popup closed by any route; null for none.</param>
        /// <exception cref="SimulatorException">A popup is already open.</exception>
        public void OpenPopup(IPopupScreen screen, Action closed)
        {
            // Allow one popup at a time, as SAP GUI does for one modal dialog level.
            if (_popup is not null)
            {
                throw new SimulatorException("The popup is already open.");
            }

            // Show the popup owned by the main window; closing it by any route clears it from the session.
            _isPopupAnswered = false;
            _popupClosed = closed;
            _popup = new ModalFrameWindow(session: this, screen)
            {
                Owner = _mainWindow
            };
            _popup.Closed += (_, _) => OnPopupClosed();
            _popup.Show();
        }

        /// <summary>
        /// Handles a virtual key sent to one of the session's windows.
        /// </summary>
        /// <param name="windowIndex">The target window index: 0 for the main window, 1 for the popup.</param>
        /// <param name="key">The SAP virtual key, such as <see cref="EnterKey"/> or <see cref="BackKey"/>.</param>
        /// <exception cref="SimulatorException">The session is busy, or the main window is blocked by the popup.</exception>
        public void SendVirtualKey(int windowIndex, int key)
        {
            // Refuse input while a round trip runs; SAP GUI does not accept keys while the session is busy.
            if (IsBusy)
            {
                throw new SimulatorException("The session is busy.");
            }

            // Route keys for the popup to the popup; the main window takes no input while the popup is open.
            if (_popup is not null)
            {
                if (windowIndex != 1)
                {
                    throw new SimulatorException("The window wnd[0] is blocked while the popup wnd[1] is open.");
                }

                SendPopupKey(key);
                return;
            }

            // Let the screen take its own keys first, so a list can use Back to leave a secondary list.
            if (_mainWindow.UserAreaContent is IVirtualKeyScreen screen && screen.TestVirtualKey(key))
            {
                screen.SendVirtualKey(key);
                return;
            }

            // Run the main window's function for the key.
            switch (key)
            {
                case EnterKey:
                    SubmitEnter();
                    break;
                case BackKey or CancelKey or ExitKey:
                    NavigateBack(isLogOff: key != CancelKey);
                    break;
                case SaveKey:
                    Save();
                    break;
                case ValueHelpKey:
                    OpenValueHelp();
                    break;
                default:
                    var text = string.Create(CultureInfo.InvariantCulture, $"Virtual key {key} is not assigned on this screen.");
                    ShowStatus(StatusMessage.NewMessage(StatusMessage.Warning, "002", text));
                    break;
            }
        }

        /// <summary>
        /// Shows a message in the status bar or, for information and termination messages, in a popup.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="isPopup">True to show success, warning, and error messages in a popup too.</param>
        /// <remarks>
        /// A message shown in a popup is also the status bar's message, marked as a popup message. A termination
        /// message ends the transaction when its popup closes, and SAP Easy Access keeps showing the message.
        /// </remarks>
        public void ShowMessage(StatusMessage message, bool isPopup)
        {
            // Show status bar messages directly; information and termination messages always need a popup.
            ArgumentNullException.ThrowIfNull(argument: message);

            if (!isPopup && !StatusMessage.TestPopupType(message.MessageType))
            {
                ShowStatus(message);
                return;
            }

            // Mark the status bar message as a popup message, then open the message popup over the main window.
            ShowStatus(message.ConvertToPopup());
            Action closed = message.MessageType == StatusMessage.Abort ? () => Navigate(ScreenCatalog.EasyAccess) : null;
            OpenPopup(new MessagePopup(session: this, message), closed);
        }

        /// <summary>
        /// Shows a message in the main window's status bar.
        /// </summary>
        /// <param name="message">The message; null clears the status bar.</param>
        public void ShowStatus(StatusMessage message)
        {
            // Keep the message for scripting reads and display it.
            Status = message ?? StatusMessage.Empty;
            _mainWindow?.ShowStatus(Status);
        }

        /// <summary>
        /// Starts a simulated long server round trip: the session reports busy until the duration has passed.
        /// </summary>
        /// <param name="duration">How long the session stays busy.</param>
        /// <param name="completed">The action to run when the round trip ends; null for none.</param>
        /// <exception cref="SimulatorException">The session is already busy.</exception>
        public void StartBusyPeriod(TimeSpan duration, Action completed)
        {
            // Allow one round trip at a time, as a SAP session processes one request at a time.
            if (IsBusy)
            {
                throw new SimulatorException("The session is busy.");
            }

            // Report busy now and end the period on the UI thread, so scripting calls keep being answered meanwhile.
            IsBusy = true;
            var timer = new DispatcherTimer { Interval = duration };
            timer.Tick += (_, _) =>
            {
                // Stop the one-shot timer, report idle, and let the screen show the round trip's result.
                timer.Stop();
                IsBusy = false;
                completed?.Invoke();
            };
            timer.Start();
        }

        /// <summary>
        /// Runs an OK code, such as <c>/nFORM</c>, the way the command field does on Enter.
        /// </summary>
        /// <param name="command">The OK code; an empty code returns to SAP Easy Access when logged on.</param>
        public void SubmitCommand(string command)
        {
            // Require a logon first, as SAP GUI does on its logon screen.
            if (Screen == ScreenCatalog.Logon)
            {
                ShowStatus(StatusMessage.NewMessage(StatusMessage.Error, "003", "Log on before you start a transaction."));
                return;
            }

            // Normalize the code: case-insensitive, with or without the /n prefix.
            var transaction = (command ?? string.Empty).Trim().ToUpperInvariant();
            if (transaction.StartsWith(NewTransactionPrefix, StringComparison.Ordinal))
            {
                transaction = transaction[NewTransactionPrefix.Length..];
            }

            // Start known transactions; report unknown ones the way SAP GUI does.
            if (transaction.Length == 0 || ScreenCatalog.TestStartable(transaction))
            {
                Navigate(transaction.Length == 0 ? ScreenCatalog.EasyAccess : transaction);
                ShowStatus(StatusMessage.Empty);
                return;
            }

            ShowStatus(StatusMessage.NewMessage(StatusMessage.Error, "004", $"Transaction {transaction} does not exist."));
        }

        /// <summary>
        /// Tests whether a window accepts a virtual key.
        /// </summary>
        /// <param name="windowIndex">The window index: 0 for the main window, 1 for the popup.</param>
        /// <param name="key">The SAP virtual key.</param>
        /// <returns>True when the key has a function in that window and the window is not blocked.</returns>
        public bool TestVirtualKey(int windowIndex, int key)
        {
            // The popup takes Enter, and Cancel when it can be cancelled; the main window takes its own and its screen's
            // keys while unblocked.
            if (_popup is not null)
            {
                return windowIndex == 1 && (key == EnterKey || (key == CancelKey && _popup.Screen.IsCancelable));
            }

            var isScreenKey = _mainWindow.UserAreaContent is IVirtualKeyScreen screen && screen.TestVirtualKey(key);

            return windowIndex == 0 && (isScreenKey || key is EnterKey or BackKey or SaveKey or CancelKey or ExitKey or ValueHelpKey);
        }

        // Goes back one level: from an application screen to SAP Easy Access, and from there to the logon screen.
        private void NavigateBack(bool isLogOff)
        {
            // Leave application screens for SAP Easy Access; leave SAP Easy Access only for Back and Exit.
            switch (Screen)
            {
                case ScreenCatalog.EasyAccess when isLogOff:
                    LogOff();
                    break;
                case ScreenCatalog.EasyAccess or ScreenCatalog.Logon:
                    ShowStatus(StatusMessage.NewMessage(StatusMessage.Warning, "006", "There is no previous screen."));
                    break;
                default:
                    Navigate(ScreenCatalog.EasyAccess);
                    ShowStatus(StatusMessage.Empty);
                    break;
            }
        }

        // Ends the session after its main window closed: closes the popup and leaves the system.
        private void OnMainWindowClosed()
        {
            // Close the popup with its owner, skipping its follow-up action, then report the session as gone.
            _popupClosed = null;
            _popup?.Close();
            _system.RemoveSession(session: this);
        }

        // Clears the popup after it closed, reports a cancellation, and runs the popup's follow-up action.
        private void OnPopupClosed()
        {
            // Forget the popup first, so the main window accepts input again.
            var isCancelled = !_isPopupAnswered && _popup.Screen.IsCancelable;
            var closed = _popupClosed;
            _popup = null;
            _popupClosed = null;

            // Report a cancellation only for popups that offer one; message popups close without an answer.
            if (isCancelled)
            {
                ShowStatus(StatusMessage.NewMessage(StatusMessage.Warning, "022", "Popup cancelled."));
            }

            closed?.Invoke();
            _mainWindow?.Activate();
        }

        // Opens the input help of the focused field, as F4 does; fields without one report a warning.
        private void OpenValueHelp()
        {
            // Find the focused field the way GuiFocus does; inner text box parts report their owning field.
            var focused = ScriptingTree.FindElement(FocusManager.GetFocusedElement(_mainWindow) as DependencyObject);
            var name = focused is null ? null : DynproField.GetValueHelp(focused);
            if (focused is not TextBox field || name is null)
            {
                ShowStatus(StatusMessage.NewMessage(StatusMessage.Warning, "007", "No input help is available for this field."));
                return;
            }

            // Show the hit list; the chosen key is written into the field.
            OpenPopup(new ValueHelpPopup(session: this, field, ValueHelpCatalog.GetValueHelp(name)));
        }

        // Saves the form; other screens have nothing to save.
        private void Save()
        {
            // Report a simulated save on the form, and a warning on every other screen.
            var message = Screen == ScreenCatalog.Form
                ? StatusMessage.NewMessage(StatusMessage.Success, "010", "Flight data saved (simulated).")
                : StatusMessage.NewMessage(StatusMessage.Warning, "011", "There is nothing to save on this screen.");
            ShowStatus(message);
        }

        // Handles the popup's keys: Enter runs the default action, Cancel (F12) closes a cancelable popup unanswered.
        private void SendPopupKey(int key)
        {
            // Run the popup keys; other keys, and Cancel on a message popup, are refused like keys with no function.
            switch (key)
            {
                case EnterKey:
                    _popup.Screen.Confirm();
                    break;
                case CancelKey when _popup.Screen.IsCancelable:
                    _popup.Close();
                    break;
                default:
                    var message = string.Create(CultureInfo.InvariantCulture, $"Virtual key {key} is not assigned on the popup.");
                    throw new SimulatorException(message);
            }
        }

        // Handles Enter on the main window: logs on from the logon screen, otherwise runs the OK code.
        private void SubmitEnter()
        {
            // Run the command field after logon; Enter with an empty command field checks the screen's entries.
            var command = _mainWindow.TakeCommand();
            if (Screen != ScreenCatalog.Logon)
            {
                if (command.Length > 0)
                {
                    SubmitCommand(command);
                }
                else if (_mainWindow.UserAreaContent is IScreenCheck check)
                {
                    ShowStatus(check.ConfirmEntries());
                }

                return;
            }

            // Log on with any user and password; the screen clears the password while it checks it.
            var logon = (LogonScreen)_mainWindow.UserAreaContent;
            if (!logon.TryTakeLogon(out var client))
            {
                ShowStatus(StatusMessage.NewMessage(StatusMessage.Error, "001", "Fill in all required entry fields."));
                return;
            }

            Client = client;
            Navigate(ScreenCatalog.EasyAccess);
            ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "000", "Logon successful (simulated)."));
        }
        #endregion
    }
}
