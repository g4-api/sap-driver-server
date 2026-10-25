using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Scripting;
using Sap.DriverServer.Settings;
using Sap.DriverServer.Settings.Models;

namespace Sap.DriverServer.Domain.UnitTests.Support
{
    /// <summary>
    /// Starts the SAP GUI simulator for the opt-in end-to-end tests and stops it when disposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The simulator runs as a separate 32-bit process and publishes <c>SAPGUI</c> in the running object table, so the
    /// tests exercise the production attach path, cross-process COM, and cross-bitness calls. The tests run only when
    /// <c>G4_SAP_SIMULATOR</c> is enabled in the shell, <c>.env</c>, or <c>appsettings.json</c>; otherwise, and when the
    /// simulator is not built or another <c>SAPGUI</c> entry is live, they report Inconclusive. A live entry is never used, because it may belong to a real SAP Logon.
    /// </para>
    /// <para>
    /// Dispose closes the session windows so the simulator revokes its entry, and kills the process only when it does
    /// not exit in time.
    /// </para>
    /// </remarks>
    public sealed class SimulatorProcess : IDisposable
    {
        #region *** Constants    ***
        // Names the variable through which dotnet test points child processes at the 64-bit runtime.
        private const string DotnetRootVariable = "DOTNET_ROOT";

        // Bounds how many windows Dispose closes before it falls back to killing the process.
        private const int MaximumCloseAttempts = 5;

        // Names the running object table entry the simulator registers.
        private const string RotEntryName = "SAPGUI";

        // Spaces the polls for the simulator's registration.
        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

        // Bounds how long the simulator may take to register.
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);
        #endregion

        #region *** Fields       ***
        // Holds the simulator process.
        private readonly Process _process;
        #endregion

        #region *** Constructors ***
        // Wraps a started simulator; created only by Start.
        private SimulatorProcess(Process process)
        {
            _process = process;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the handle of the simulator's first session window; zero before it is shown.
        /// </summary>
        public long MainWindowHandle
        {
            get
            {
                // Refresh the cached process data; the window appears after the process starts.
                _process.Refresh();

                return _process.MainWindowHandle.ToInt64();
            }
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // Close the session windows one by one; the simulator exits after the last and revokes its entry.
            for (var attempt = 0; attempt < MaximumCloseAttempts && !_process.HasExited; attempt++)
            {
                _process.Refresh();
                if (!_process.CloseMainWindow())
                {
                    break;
                }

                _process.WaitForExit(milliseconds: 2000);
            }

            // Kill a simulator that did not exit; the driver skips the entry it leaves until Windows removes it.
            if (!_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(milliseconds: 5000);
            }

            _process.Dispose();
        }

        /// <summary>
        /// Starts the simulator and waits until it registered <c>SAPGUI</c>.
        /// </summary>
        /// <param name="arguments">The simulator arguments, such as <c>--sessions</c> and <c>2</c>; <c>--quiet</c> is always added.</param>
        /// <returns>The running simulator; the caller disposes it.</returns>
        public static SimulatorProcess Start(params string[] arguments)
        {
            // Skip unless opted in, built, and alone in the running object table.
            if (!AppSettings.Environment.IsSapSimulatorEnabled)
            {
                Assert.Inconclusive($"Set {EnvironmentSettings.SapSimulatorKey}=true to run the SAP GUI simulator tests.");
            }

            var path = GetExecutablePath();
            if (!File.Exists(path))
            {
                Assert.Inconclusive($"The SAP GUI simulator is not built at '{path}'. Build the solution or set {EnvironmentSettings.SapSimulatorPathKey}.");
            }

            if (TestRotEntry())
            {
                Assert.Inconclusive("SAP GUI or another simulator is running; the simulator tests use the SAPGUI entry exclusively.");
            }

            // Start the simulator without message boxes, so a failure never waits for input.
            var startInfo = new ProcessStartInfo(path)
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--quiet");

            // Drop the 64-bit runtime location the test host passes down; the 32-bit simulator finds its own runtime.
            startInfo.Environment.Remove(DotnetRootVariable);

            foreach (var argument in arguments ?? [])
            {
                startInfo.ArgumentList.Add(argument);
            }

            var simulator = new SimulatorProcess(Process.Start(startInfo));
            simulator.WaitForRegistration();

            return simulator;
        }

        // Resolves the simulator executable: the override, or the build output next to this test project's output.
        private static string GetExecutablePath()
        {
            // Prefer an explicit path, such as a published simulator.
            var configuredPath = AppSettings.Environment.SapSimulatorPath;
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            // Map src\<tests>\bin\<configuration>\<framework> to src\Sap.DriverServer.Simulator\bin\<configuration>\<framework>.
            var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            var configuration = outputDirectory.Parent.Name;
            var sourceDirectory = outputDirectory.Parent.Parent.Parent.Parent.FullName;

            return Path.Combine(sourceDirectory, "Sap.DriverServer.Simulator", "bin", configuration, outputDirectory.Name, "Sap.DriverServer.Simulator.exe");
        }

        // Tests whether a live process registered SAPGUI, releasing the entry it binds to.
        private static bool TestRotEntry()
        {
            // Bind to the entry; entries of exited processes do not bind and count as absent.
            var entry = new RunningObjectTableReader().GetObject(RotEntryName);
            if (entry is null)
            {
                return false;
            }

            if (Marshal.IsComObject(entry))
            {
                Marshal.ReleaseComObject(entry);
            }

            return true;
        }

        // Waits until the simulator registered SAPGUI; fails the test when it exits or takes too long.
        private void WaitForRegistration()
        {
            var stopwatch = Stopwatch.StartNew();

            while (!TestRotEntry())
            {
                // Report an early exit with its code, which names the reason (2 arguments, 3 entry taken, 4 COM).
                if (_process.HasExited)
                {
                    Assert.Fail($"The SAP GUI simulator exited with code {_process.ExitCode} before it registered.");
                }

                if (stopwatch.Elapsed > StartTimeout)
                {
                    Dispose();
                    Assert.Fail($"The SAP GUI simulator did not register within {StartTimeout.TotalSeconds} seconds.");
                }

                Thread.Sleep(PollInterval);
            }
        }
        #endregion
    }
}
