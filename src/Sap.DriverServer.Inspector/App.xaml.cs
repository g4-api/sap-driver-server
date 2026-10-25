using System.Windows;

using Microsoft.Extensions.Logging.Abstractions;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Inspector.ViewModels;
using Sap.DriverServer.Inspector.Views;
using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;

namespace Sap.DriverServer.Inspector
{
    /// <summary>
    /// Composes the inspector: one scripting host (STA dispatcher and COM engine), the local source, the view model, and the window.
    /// </summary>
    /// <remarks>
    /// The inspector starts without SAP GUI and without a driver: the scripting host attaches lazily through the running
    /// object table, and driver sessions are added later from the Create session dialog. The app owns the scripting host
    /// for its lifetime and disposes it on exit, after the view model has removed any SAP GUI frame and deleted its
    /// owned driver sessions, so COM references are released on their own thread before the process ends.
    /// </remarks>
    public partial class App : Application
    {
        #region *** Fields       ***
        // Owns the STA dispatcher and the SAP GUI Scripting engine.
        private SapScriptingHost _host;

        // Owns the overlays and the SAP GUI frame; disposed before the host.
        private MainViewModel _viewModel;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override void OnExit(ExitEventArgs e)
        {
            // Remove SAP GUI's frame and the overlays, then release the COM engine and stop the dispatcher.
            _viewModel?.Dispose();
            _host?.Dispose();
            base.OnExit(e);
        }

        /// <inheritdoc />
        protected override void OnStartup(StartupEventArgs e)
        {
            // Use short budgets: an inspector waits for SAP GUI interactively, never for minutes.
            base.OnStartup(e);
            var settings = new SapDriverSettings
            {
                BusyWaitMilliseconds = 3000,
                ComCallTimeoutMilliseconds = 15000
            };

            // Compose the inspector around one shared scripting host.
            _host = new SapScriptingHost(new StaComDispatcher(), new ComSapGuiEngine(), settings, NullLogger<SapScriptingHost>.Instance);
            _viewModel = new MainViewModel(new LocalInspectorSource(new InspectorService(_host)), new Highlighter(), new CapabilitySetStore());

            MainWindow = new MainWindow(_viewModel);
            MainWindow.Show();
        }
        #endregion
    }
}
