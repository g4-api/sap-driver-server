using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Settings;
using Sap.DriverServer.Settings.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    public class AppSettingsTests
    {
        // Holds a settings file whose Environment section enables the simulator and names a simulator path.
        private const string SettingsJson = """
            {
                "Environment": {
                    "G4_SAP_SIMULATOR": true,
                    "G4_SAP_SIMULATOR_PATH": "C:\\json\\simulator.exe"
                }
            }
            """;

        // Holds the per-test folder that plays the application folder; deleted after each test.
        private string _directory;

        [TestInitialize]
        public void Initialize()
        {
            // Give each test an empty application folder, so files from other tests never leak in.
            _directory = Path.Combine(Path.GetTempPath(), $"sap-settings-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Remove the folder and every file the test wrote.
            Directory.Delete(_directory, recursive: true);
        }

        [TestMethod(DisplayName = "Verify that the Environment section of appsettings.json supplies the values when nothing overrides them.")]
        public void SettingsFileTest()
        {
            // Arrange: only the settings file exists.
            File.WriteAllText(Path.Combine(_directory, AppSettings.SettingsFileName), SettingsJson);

            // Act: resolve without shell variables.
            var settings = NewEnvironmentSettings([]);

            // Assert: both keys come from the section; absent keys keep their defaults.
            Assert.IsTrue(settings.IsSapSimulatorEnabled);
            Assert.AreEqual(@"C:\json\simulator.exe", settings.SapSimulatorPath);
            Assert.IsFalse(settings.IsSapIntegrationEnabled);
            Assert.IsNull(settings.SapDriverPath);
        }

        [TestMethod(DisplayName = "Verify that .env wins over appsettings.json for the keys it sets.")]
        public void EnvironmentFileOverridesSettingsFileTest()
        {
            // Arrange: .env turns the simulator off and leaves the path to the settings file.
            File.WriteAllText(Path.Combine(_directory, AppSettings.SettingsFileName), SettingsJson);
            File.WriteAllText(Path.Combine(_directory, AppSettings.EnvironmentFileName), "G4_SAP_SIMULATOR=false");

            // Act: resolve without shell variables.
            var settings = NewEnvironmentSettings([]);

            // Assert: .env decides the switch; the settings file still supplies the path.
            Assert.IsFalse(settings.IsSapSimulatorEnabled);
            Assert.AreEqual(@"C:\json\simulator.exe", settings.SapSimulatorPath);
        }

        [TestMethod(DisplayName = "Verify that the shell wins over both .env and appsettings.json.")]
        public void ShellOverridesFilesTest()
        {
            // Arrange: both files set the path; the shell sets it too.
            File.WriteAllText(Path.Combine(_directory, AppSettings.SettingsFileName), SettingsJson);
            File.WriteAllText(Path.Combine(_directory, AppSettings.EnvironmentFileName), @"G4_SAP_SIMULATOR_PATH=C:\env\simulator.exe");
            var shellVariables = new Dictionary<string, string>
            {
                [EnvironmentSettings.SapSimulatorPathKey] = @"C:\shell\simulator.exe"
            };

            // Act: resolve with the shell variable.
            var settings = NewEnvironmentSettings(shellVariables);

            // Assert: the shell value is used.
            Assert.AreEqual(@"C:\shell\simulator.exe", settings.SapSimulatorPath);
        }

        [TestMethod(DisplayName = "Verify that missing files leave every setting at its default.")]
        public void MissingFilesTest()
        {
            // Act: resolve from an empty folder without shell variables.
            var settings = NewEnvironmentSettings([]);

            // Assert: the switches are off and the paths are unset.
            Assert.IsFalse(settings.IsSapIntegrationEnabled);
            Assert.IsFalse(settings.IsSapSimulatorEnabled);
            Assert.IsNull(settings.SapDriverPath);
            Assert.IsNull(settings.SapSimulatorPath);
        }

        #region *** Data Set ***
        [DataRow("1", true)]
        [DataRow("0", false)]
        [DataRow("true", true)]
        [DataRow("False", false)]
        [DataRow(" 1 ", true)]
        #endregion
        [TestMethod(DisplayName = "Verify that switches accept 1 and 0 as well as true and false.")]
        public void SwitchValueTest(string value, bool expected)
        {
            // Arrange: set the switch in the shell, where existing instructions use 1.
            var shellVariables = new Dictionary<string, string>
            {
                [EnvironmentSettings.SapIntegrationKey] = value
            };

            // Act: resolve the settings.
            var settings = NewEnvironmentSettings(shellVariables);

            // Assert: the value maps to the expected switch state.
            Assert.AreEqual(expected, settings.IsSapIntegrationEnabled);
        }

        [TestMethod(DisplayName = "Verify that .env names with a double underscore map to configuration sections, as shell names do.")]
        public void SectionKeyTest()
        {
            // Arrange: set a driver setting through .env.
            File.WriteAllText(Path.Combine(_directory, AppSettings.EnvironmentFileName), "SapDriver__BusyWaitMilliseconds=5000");

            // Act: build the configuration without shell variables.
            var configuration = AppSettings.NewConfiguration(_directory, new Dictionary<string, string>());

            // Assert: the value sits under the section key.
            Assert.AreEqual("5000", configuration["SapDriver:BusyWaitMilliseconds"]);
        }

        [TestMethod(DisplayName = "Verify that the .env reader skips comments and blank lines, ignores export, strips quotes, and keeps the last duplicate.")]
        public void ReadEnvironmentFileTest()
        {
            // Arrange: one line per rule.
            var path = Path.Combine(_directory, AppSettings.EnvironmentFileName);
            File.WriteAllLines(path,
            [
                "# Comment",
                string.Empty,
                "export EXPORTED=yes",
                "DOUBLE=\"two words\"",
                "SINGLE='single'",
                "EQUALS=a=b",
                "EMPTY=",
                "  SPACED  =  value  ",
                "LONE=\"open",
                "REPEATED=first",
                "REPEATED=last"
            ]);

            // Act: read the file.
            var values = AppSettings.ReadEnvironmentFile(path);

            // Assert: each rule produced its value.
            Assert.HasCount(8, values);
            Assert.AreEqual("yes", values["EXPORTED"]);
            Assert.AreEqual("two words", values["DOUBLE"]);
            Assert.AreEqual("single", values["SINGLE"]);
            Assert.AreEqual("a=b", values["EQUALS"]);
            Assert.AreEqual(string.Empty, values["EMPTY"]);
            Assert.AreEqual("value", values["spaced"]);
            Assert.AreEqual("\"open", values["LONE"]);
            Assert.AreEqual("last", values["REPEATED"]);
        }

        [TestMethod(DisplayName = "Verify that a malformed .env line fails with its line number and without its content.")]
        public void MalformedLineTest()
        {
            // Arrange: the second line has no key.
            var path = Path.Combine(_directory, AppSettings.EnvironmentFileName);
            File.WriteAllLines(path, ["G4_SAP_SIMULATOR=true", "secret-value"]);

            // Act: read the file.
            var exception = Assert.ThrowsExactly<FormatException>(() => AppSettings.ReadEnvironmentFile(path));

            // Assert: the message locates the line and never echoes it.
            Assert.Contains("Line 2", exception.Message);
            Assert.DoesNotContain("secret-value", exception.Message);
        }

        // Resolves the settings of the test folder with the given shell variables.
        private EnvironmentSettings NewEnvironmentSettings(Dictionary<string, string> shellVariables)
        {
            // Build the layered configuration and bind it, exactly as the process-wide settings are built.
            var configuration = AppSettings.NewConfiguration(_directory, shellVariables);

            return AppSettings.NewEnvironmentSettings(configuration);
        }
    }
}
