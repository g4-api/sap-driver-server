using Microsoft.Extensions.Logging.Abstractions;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;

namespace Sap.DriverServer.Domain.UnitTests.Support
{
    /// <summary>
    /// Creates production <see cref="SapScriptingHost"/> instances over a real STA dispatcher and a fake engine.
    /// </summary>
    /// <remarks>
    /// Tests exercise the real threading, timeout, and failure-classification path; only the COM layer is faked.
    /// Callers dispose the returned host, which stops its dispatcher thread.
    /// </remarks>
    public static class TestHosts
    {
        #region *** Methods      ***
        /// <summary>
        /// Creates the settings used by tests: a short busy wait and COM timeout so failure tests finish quickly.
        /// </summary>
        /// <returns>The settings.</returns>
        public static SapDriverSettings NewSettings()
        {
            // Keep budgets small; fake operations complete in microseconds.
            return new SapDriverSettings
            {
                BusyWaitMilliseconds = 300,
                ComCallTimeoutMilliseconds = 2000
            };
        }

        /// <summary>
        /// Creates a scripting host over a fake engine.
        /// </summary>
        /// <param name="engine">The fake engine.</param>
        /// <returns>The host; the caller disposes it.</returns>
        public static SapScriptingHost NewHost(FakeSapGuiEngine engine)
        {
            // Use the production dispatcher so operations run on a real STA thread.
            return new SapScriptingHost(new StaComDispatcher(), engine, NewSettings(), NullLogger<SapScriptingHost>.Instance);
        }
        #endregion
    }
}
