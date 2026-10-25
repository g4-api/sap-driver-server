using System;
using System.Windows;

using Sap.DriverServer.Inspector.ViewModels;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Hosts the Create session dialog, which connects the inspector to a SAP driver server like Appium Inspector does.
    /// </summary>
    /// <remarks>
    /// The dialog closes with a true result once the view model completes, leaving the new source in
    /// <see cref="CreateSessionViewModel.Result"/>; Cancel closes it with a false result and no source.
    /// </remarks>
    public partial class CreateSessionWindow : Window
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="CreateSessionWindow"/> class.
        /// </summary>
        /// <param name="viewModel">The dialog view model.</param>
        public CreateSessionWindow(CreateSessionViewModel viewModel)
        {
            // Bind the view model and close with success when it completes.
            ArgumentNullException.ThrowIfNull(argument: viewModel);

            // Build the view with a G4 title bar, matching the main window.
            InitializeComponent();
            TitleBarTheme.Apply(this);
            DataContext = viewModel;
            viewModel.Completed += (_, _) =>
            {
                // A session that completes after Cancel has no taker; release it so an owned session is deleted.
                if (IsVisible)
                {
                    DialogResult = true;
                    return;
                }

                viewModel.Result?.Dispose();
            };

            // Read the local sessions once the dialog shows, so the prefill picker is ready.
            Loaded += (_, _) =>
            {
                if (viewModel.RefreshLocalSessionsCommand.CanExecute(parameter: null))
                {
                    viewModel.RefreshLocalSessionsCommand.Execute(parameter: null);
                }
            };
        }
        #endregion
    }
}
