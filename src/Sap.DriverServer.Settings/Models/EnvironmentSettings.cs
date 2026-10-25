using Microsoft.Extensions.Configuration;

namespace Sap.DriverServer.Settings.Models
{
    /// <summary>
    /// Represents the <c>Environment</c> section of <c>appsettings.json</c>: the switches and paths the tests read.
    /// </summary>
    /// <remarks>
    /// Each property keeps its environment variable name as its configuration key, so the same name works in the shell,
    /// in <c>.env</c>, and in the section. <see cref="AppSettings"/> resolves them in that order: the shell wins over
    /// <c>.env</c>, which wins over <c>appsettings.json</c>.
    /// </remarks>
    public class EnvironmentSettings
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the key of the published driver executable path.
        /// </summary>
        public const string SapDriverPathKey = "G4_SAP_DRIVER_PATH";

        /// <summary>
        /// Specifies the key of the switch that enables the live SAP GUI tests.
        /// </summary>
        public const string SapIntegrationKey = "G4_SAP_INTEGRATION";

        /// <summary>
        /// Specifies the key of the switch that enables the SAP GUI simulator tests.
        /// </summary>
        public const string SapSimulatorKey = "G4_SAP_SIMULATOR";

        /// <summary>
        /// Specifies the key of the simulator executable path.
        /// </summary>
        public const string SapSimulatorPathKey = "G4_SAP_SIMULATOR_PATH";
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets a value indicating whether the live SAP GUI tests run.
        /// </summary>
        /// <remarks>
        /// They attach to the SAP GUI running on the machine, so they stay off unless enabled.
        /// </remarks>
        [ConfigurationKeyName(SapIntegrationKey)]
        public bool IsSapIntegrationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the SAP GUI simulator tests run.
        /// </summary>
        /// <remarks>
        /// They start the simulator, which opens windows on the desktop, so they stay off unless enabled.
        /// </remarks>
        [ConfigurationKeyName(SapSimulatorKey)]
        public bool IsSapSimulatorEnabled { get; set; }

        /// <summary>
        /// Gets or sets the published driver executable the published-driver test starts.
        /// </summary>
        /// <remarks>
        /// An empty value selects the driver in the repository's <c>artifacts</c> folder.
        /// </remarks>
        [ConfigurationKeyName(SapDriverPathKey)]
        public string SapDriverPath { get; set; }

        /// <summary>
        /// Gets or sets the simulator executable the simulator tests start.
        /// </summary>
        /// <remarks>
        /// An empty value selects the simulator in the solution's build output.
        /// </remarks>
        [ConfigurationKeyName(SapSimulatorPathKey)]
        public string SapSimulatorPath { get; set; }
        #endregion
    }
}
