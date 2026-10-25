using System;
using System.Runtime.InteropServices;
using System.Windows;

using Sap.DriverServer.Simulator.Hosting;
using Sap.DriverServer.Simulator.Scripting;
using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator
{
    /// <summary>
    /// Starts the SAP GUI simulator: opens the sessions and publishes the scripting engine as <c>SAPGUI</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The simulator is a test double for SAP Logon. It lets the driver and the inspector be exercised end to end, on a
    /// machine without SAP GUI, through the same running object table entry and COM calls they use with SAP GUI.
    /// </para>
    /// <para>
    /// Exit codes: 0 after the last session closed, 2 for invalid arguments, 3 when a live process already registered
    /// <c>SAPGUI</c> (a real SAP Logon or another simulator), and 4 when COM refused the registration. Failures are
    /// shown in a message box unless <c>--quiet</c> is given.
    /// </para>
    /// </remarks>
    public partial class App : Application
    {
        #region *** Constants    ***
        // Signals that a live process already owns the SAPGUI entry.
        private const int EntryTakenExitCode = 3;

        // Signals invalid command-line arguments.
        private const int InvalidArgumentsExitCode = 2;

        // Signals that COM refused the registration.
        private const int RegistrationFailedExitCode = 4;

        // Names the running object table entry SAP Logon registers.
        private const string RotEntryName = "SAPGUI";
        #endregion

        #region *** Fields       ***
        // Revokes the SAPGUI entry on exit; null until the registration succeeded.
        private RunningObjectTableRegistration _registration;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override void OnExit(ExitEventArgs e)
        {
            // Revoke the entry first, so clients stop finding a simulator that is shutting down.
            _registration?.Dispose();
            base.OnExit(e);
        }

        /// <inheritdoc />
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Read the scenario; invalid arguments end the process before anything is shown.
            SimulatorOptions options;
            try
            {
                options = SimulatorOptions.ConvertFromArguments(e.Args);
            }
            catch (ArgumentException exception)
            {
                Fail(isQuiet: false, exception.Message, InvalidArgumentsExitCode);
                return;
            }

            // Open the sessions first, so the scripting tree is complete when the first client attaches.
            var system = new SimulatedSystem(options);
            system.AllSessionsClosed += (_, _) => Shutdown();
            system.Start();

            try
            {
                // Publish the engine on this UI thread, so every scripting call is delivered to it.
                _registration = RunningObjectTableRegistration.Register(RotEntryName, new SapGuiEntry(system));
            }
            catch (InvalidOperationException exception)
            {
                Fail(options.IsQuiet, exception.Message, EntryTakenExitCode);
            }
            catch (COMException exception)
            {
                Fail(options.IsQuiet, exception.Message, RegistrationFailedExitCode);
            }
        }

        // Reports a startup failure and ends the process with the failure's exit code.
        private void Fail(bool isQuiet, string message, int exitCode)
        {
            // Show the reason to an interactive user; unattended runs read the exit code instead.
            if (!isQuiet)
            {
                MessageBox.Show(message, "SAP GUI Simulator", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Shutdown(exitCode);
        }
        #endregion
    }
}
