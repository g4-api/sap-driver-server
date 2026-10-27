using System;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the confirmation popup content: a question with Yes and No option buttons in the user area.
    /// </summary>
    /// <remarks>
    /// The popup has no toolbar; Enter chooses <see cref="DefaultAnswer"/>, and F12 or Escape cancel it.
    /// </remarks>
    public partial class ConfirmPopup : UserControl, IPopupScreen
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the answer Enter chooses: the first option.
        /// </summary>
        public const string DefaultAnswer = "Yes";

        // Names the second option's answer.
        private const string SecondAnswer = "No";
        #endregion

        #region *** Fields       ***
        // Receives the answer, reports it, and closes the popup.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfirmPopup"/> class.
        /// </summary>
        /// <param name="session">The session that receives the answer.</param>
        public ConfirmPopup(SimulatedSession session)
        {
            // Require the session that receives the answer, then build the content.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public string ConfirmText => null;

        /// <inheritdoc />
        public bool IsCancelable => true;

        /// <inheritdoc />
        public string Title => "Confirmation";
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Confirm()
        {
            // Answer with the first option, as Enter does in SAP's confirmation popup.
            _session.AnswerPopup(DefaultAnswer);
        }

        // Answers with the second option.
        private void OnNoClick(object sender, RoutedEventArgs e)
        {
            // Answer No; the session reports the answer and closes the popup.
            _session.AnswerPopup(SecondAnswer);
        }

        // Answers with the first option.
        private void OnYesClick(object sender, RoutedEventArgs e)
        {
            // Answer Yes; the session reports the answer and closes the popup.
            _session.AnswerPopup(DefaultAnswer);
        }
        #endregion
    }
}
