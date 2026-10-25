using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the confirmation popup (<c>wnd[1]</c>) with a question and two options.
    /// </summary>
    /// <remarks>
    /// The popup is shown modeless and the session blocks <c>wnd[0]</c> while it is open, so a scripting call that
    /// opens it returns at once instead of waiting in a modal message loop. Enter chooses <see cref="DefaultAnswer"/>;
    /// Escape, F12, and the close button cancel it.
    /// </remarks>
    public partial class ModalFrameWindow : FrameWindow
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the answer Enter chooses: the first option.
        /// </summary>
        public const string DefaultAnswer = "Yes";

        // Names the second option's answer.
        private const string SecondAnswer = "No";
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ModalFrameWindow"/> class.
        /// </summary>
        /// <param name="session">The session that owns the popup.</param>
        public ModalFrameWindow(SimulatedSession session)
            : base(session)
        {
            InitializeComponent();
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public override FrameworkElement ContentRoot => RootGrid;

        /// <inheritdoc />
        public override string ScreenTitle => "Confirmation";

        /// <inheritdoc />
        public override int WindowIndex => 1;

        /// <inheritdoc />
        protected override Canvas HighlightLayer => Highlights;
        #endregion

        #region *** Methods      ***
        // Answers with the second option.
        private void OnNoClick(object sender, RoutedEventArgs e)
        {
            // Answer No; the session reports the answer and closes the popup.
            Session.AnswerPopup(SecondAnswer);
        }

        // Turns Enter, Escape, and F12 into the popup's virtual keys.
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
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

            // Consume the key and let the session run it; refused keys only beep.
            e.Handled = true;
            try
            {
                Session.SendVirtualKey(WindowIndex, key);
            }
            catch (SimulatorException)
            {
                SystemSounds.Beep.Play();
            }
        }

        // Answers with the first option.
        private void OnYesClick(object sender, RoutedEventArgs e)
        {
            // Answer Yes; the session reports the answer and closes the popup.
            Session.AnswerPopup(DefaultAnswer);
        }
        #endregion
    }
}
