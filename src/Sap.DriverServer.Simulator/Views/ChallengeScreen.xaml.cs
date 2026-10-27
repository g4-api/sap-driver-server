using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the challenge test lab (transaction <c>ZSIM_CHALLENGE</c>): timing, state, and identity traps on one screen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The traps:
    /// </para>
    /// <list type="bullet">
    /// <item><description><c>btnCHALLENGE_DELAYED</c> appears 2 seconds after the screen opens; a lookup right away finds nothing.</description></item>
    /// <item><description><c>btnCHALLENGE_BUSY</c> keeps the session busy for 1.5 seconds; virtual keys are refused meanwhile.</description></item>
    /// <item><description><c>btnCHALLENGE_POST</c> asks for confirmation on some presses only: the second, third, and fifth of every eight.</description></item>
    /// <item><description>Two buttons share the text <c>Select</c>, so a locator by text is ambiguous.</description></item>
    /// <item><description>The mode radio buttons replace the detail subscreen, so the value field's identifier depends on the choice.</description></item>
    /// <item><description>Enter checks the date (error) before the quantity (warning): an error locks every other field until it is fixed, and a quantity above 100 needs a second Enter.</description></item>
    /// <item><description><c>btnCHALLENGE_HIDDEN</c> sits below the visible part of a scroll container; scripting can press it, a click at its coordinates cannot.</description></item>
    /// </list>
    /// <para>
    /// The state resets whenever the transaction starts again. The behavior follows the SAP GUI Scripting documentation
    /// and scripting experience; it has not been compared with a live SAP system.
    /// </para>
    /// </remarks>
    public partial class ChallengeScreen : UserControl, IScreenCheck
    {
        #region *** Constants    ***
        // Sets how long the long task keeps the session busy.
        private static readonly TimeSpan BusyDuration = TimeSpan.FromMilliseconds(1500);

        // Sets how long the delayed button stays hidden after the screen opens.
        private static readonly TimeSpan DelayedButtonDelay = TimeSpan.FromSeconds(2);

        // Decides, press by press, whether Post asks for confirmation: irregular for a script, the same on every run.
        private static readonly bool[] PostPattern = [false, true, true, false, true, false, false, true];

        // Sets the quantity above which the check asks for confirmation.
        private const int QuantityWarningLimit = 100;
        #endregion

        #region *** Fields       ***
        // Shows the delayed button once its delay has passed; stopped when the screen closes.
        private readonly DispatcherTimer _delayTimer;

        // Holds the quantity the last warning was raised for; a second Enter with the same value confirms it.
        private string _pendingQuantity;

        // Counts the post presses, which select the next entry of the post pattern.
        private int _postCount;

        // Reports results and opens popups.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ChallengeScreen"/> class.
        /// </summary>
        /// <param name="session">The session that reports results, runs busy periods, and opens popups.</param>
        public ChallengeScreen(SimulatedSession session)
        {
            // Require the session, then build the screen.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();

            // Show the delayed button after its delay, once.
            _delayTimer = new DispatcherTimer { Interval = DelayedButtonDelay };
            _delayTimer.Tick += (_, _) =>
            {
                // Stop the one-shot timer and reveal the button; scripting sees it from now on.
                _delayTimer.Stop();
                DelayedButton.Visibility = Visibility.Visible;
            };
            _delayTimer.Start();
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public StatusMessage ConfirmEntries()
        {
            // Check the date first: an error locks every other field, as an error message does in SAP.
            const string dateFormat = "yyyy-MM-dd";
            var isDateValid = DateTime.TryParseExact(DateField.Text.Trim(), dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
            SetOtherFieldsEnabled(isDateValid);
            if (!isDateValid)
            {
                DateField.Focus();

                return StatusMessage.NewMessage(StatusMessage.Error, "113", "Enter the delivery date as YYYY-MM-DD.");
            }

            // Warn once about a high quantity; a second Enter with the same value confirms it.
            var quantity = QuantityField.Text.Trim();
            var isNumber = int.TryParse(quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);
            var isHigh = isNumber && value > QuantityWarningLimit;
            if (isHigh && quantity != _pendingQuantity)
            {
                _pendingQuantity = quantity;
                QuantityField.Focus();

                return StatusMessage.NewMessage(StatusMessage.Warning, "115", "The quantity is unusually high; press Enter to confirm.");
            }

            _pendingQuantity = null;

            return StatusMessage.NewMessage(StatusMessage.Success, "116", "Entries accepted (simulated).");
        }

        // Starts the long task; the session stays busy until it ends.
        private void OnBusyClick(object sender, RoutedEventArgs e)
        {
            // Run the busy period and report its end.
            var message = StatusMessage.NewMessage(StatusMessage.Success, "111", "Long task finished.");
            _session.StartBusyPeriod(BusyDuration, () => _session.ShowStatus(message));
        }

        // Reports the delayed button.
        private void OnDelayedClick(object sender, RoutedEventArgs e)
        {
            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "110", "Delayed button pressed."));
        }

        // Reports the hidden button, which scripting reaches even when it is scrolled out of view.
        private void OnHiddenClick(object sender, RoutedEventArgs e)
        {
            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "114", "Hidden action pressed."));
        }

        // Swaps the detail subscreen to match the chosen mode, which changes the value field's identifier.
        private void OnModeChecked(object sender, RoutedEventArgs e)
        {
            // Ignore the events raised while the screen is still being built.
            if (DetailA is null || DetailB is null)
            {
                return;
            }

            // Show the subscreen of the chosen mode; the hidden one leaves the scripting tree with its field.
            var isModeA = ModeA.IsChecked == true;
            DetailA.Visibility = isModeA ? Visibility.Visible : Visibility.Collapsed;
            DetailB.Visibility = isModeA ? Visibility.Collapsed : Visibility.Visible;
        }

        // Posts, asking for confirmation on some presses only.
        private void OnPostClick(object sender, RoutedEventArgs e)
        {
            // Let the pattern decide; a script has to check for wnd[1] after every press.
            if (PostPattern[_postCount++ % PostPattern.Length])
            {
                _session.OpenPopup(new ConfirmPopup(_session));
                return;
            }

            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "112", "Posted without confirmation."));
        }

        // Reports which of the two Select buttons was pressed.
        private void OnSelectClick(object sender, RoutedEventArgs e)
        {
            // Name the button by its tag, so tests can tell the two identical texts apart.
            var name = Convert.ToString(((FrameworkElement)sender).Tag, CultureInfo.InvariantCulture);
            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "117", $"Select {name} pressed."));
        }

        // Stops the delay timer when the screen closes, so a closed screen changes nothing.
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _delayTimer.Stop();
        }

        // Locks or unlocks every input field except the date, as SAP does after an error message.
        private void SetOtherFieldsEnabled(bool isEnabled)
        {
            // Change the fields that the error does not concern; the date stays open for correction.
            QuantityField.IsEnabled = isEnabled;
            AlphaField.IsEnabled = isEnabled;
            BetaField.IsEnabled = isEnabled;
            ModeA.IsEnabled = isEnabled;
            ModeB.IsEnabled = isEnabled;
        }
        #endregion
    }
}
