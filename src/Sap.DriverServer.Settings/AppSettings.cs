using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Microsoft.Extensions.Configuration;

using Sap.DriverServer.Settings.Models;

namespace Sap.DriverServer.Settings
{
    /// <summary>
    /// Provides the application configuration and the typed <c>Environment</c> settings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The configuration reads <c>appsettings.json</c> and <c>.env</c> from the application folder, then the shell
    /// environment. A later source wins, so the shell overrides both files: a container or a CI job always gets the values
    /// it sets. A missing file contributes nothing.
    /// </para>
    /// <para>
    /// <c>.env</c> holds <c>KEY=VALUE</c> lines. Values are never logged or echoed in error messages.
    /// </para>
    /// </remarks>
    public static class AppSettings
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the name of the environment file read from the application folder.
        /// </summary>
        public const string EnvironmentFileName = ".env";

        /// <summary>
        /// Specifies the <c>appsettings.json</c> section that holds the environment keys.
        /// </summary>
        public const string EnvironmentSectionName = "Environment";

        /// <summary>
        /// Specifies the name of the settings file read from the application folder.
        /// </summary>
        public const string SettingsFileName = "appsettings.json";

        // Marks an environment file line that sets a shell variable; the prefix is accepted and ignored.
        private const string ExportPrefix = "export ";

        // Separates configuration path segments in environment variable names, as in SapDriver__BusyWaitMilliseconds.
        private const string KeyDelimiter = "__";
        #endregion

        #region *** Fields       ***
        /// <summary>
        /// Gets the configuration of this process, built from its application folder and the shell environment.
        /// </summary>
        public static readonly IConfigurationRoot Configuration = NewConfiguration(baseDirectory: AppContext.BaseDirectory);

        /// <summary>
        /// Gets the environment settings of this process, resolved from <see cref="Configuration"/>.
        /// </summary>
        public static readonly EnvironmentSettings Environment = NewEnvironmentSettings(configuration: Configuration);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Creates a configuration from the files in a folder and the shell environment of this process.
        /// </summary>
        /// <param name="baseDirectory">The folder that holds <c>appsettings.json</c> and <c>.env</c>.</param>
        /// <returns>The configuration; the shell wins over <c>.env</c>, which wins over <c>appsettings.json</c>.</returns>
        public static IConfigurationRoot NewConfiguration(string baseDirectory)
        {
            // Copy the shell environment; the process table is not a string dictionary, and on Linux two names may differ
            // only in case, so the last one wins instead of failing.
            var shellVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry variable in System.Environment.GetEnvironmentVariables())
            {
                shellVariables[(string)variable.Key] = (string)variable.Value;
            }

            return NewConfiguration(baseDirectory: baseDirectory, shellVariables: shellVariables);
        }

        /// <summary>
        /// Creates a configuration from the files in a folder and the given shell variables.
        /// </summary>
        /// <param name="baseDirectory">The folder that holds <c>appsettings.json</c> and <c>.env</c>.</param>
        /// <param name="shellVariables">The shell variables, which win over both files.</param>
        /// <returns>The configuration; the shell wins over <c>.env</c>, which wins over <c>appsettings.json</c>.</returns>
        public static IConfigurationRoot NewConfiguration(string baseDirectory, IDictionary<string, string> shellVariables)
        {
            // Require both inputs; the folder anchors the files and the shell variables form the top layer.
            AssertArguments(baseDirectory, shellVariables);

            // Read the environment file; a missing file yields no values.
            var fileVariables = ReadEnvironmentFile(path: Path.Combine(baseDirectory, EnvironmentFileName));

            // Layer the sources from weakest to strongest; each later source overrides the earlier ones per key.
            return new ConfigurationBuilder()
                .SetBasePath(baseDirectory)
                .AddJsonFile(path: SettingsFileName, optional: true, reloadOnChange: false)
                .AddInMemoryCollection(ConvertToConfigurationValues(fileVariables))
                .AddInMemoryCollection(ConvertToConfigurationValues(shellVariables))
                .Build();

            // Rejects a blank folder and missing shell variables before any file is read.
            static void AssertArguments(string baseDirectory, IDictionary<string, string> shellVariables)
            {
                // Check the folder first; it is the input a caller most often leaves empty.
                ArgumentException.ThrowIfNullOrWhiteSpace(argument: baseDirectory);
                ArgumentNullException.ThrowIfNull(argument: shellVariables);
            }
        }

        /// <summary>
        /// Resolves the environment settings from a configuration.
        /// </summary>
        /// <param name="configuration">The configuration, usually <see cref="Configuration"/>.</param>
        /// <returns>The settings; keys absent from every source keep their defaults.</returns>
        /// <remarks>
        /// The section supplies the <c>appsettings.json</c> values. The shell and <c>.env</c> set the same keys at the
        /// configuration root, so binding the root second lets them win.
        /// </remarks>
        public static EnvironmentSettings NewEnvironmentSettings(IConfiguration configuration)
        {
            // Require the configuration the settings come from.
            ArgumentNullException.ThrowIfNull(argument: configuration);

            // Bind the section first and the root second; the binder sets only the keys a source contains.
            var settings = new EnvironmentSettings();
            configuration.GetSection(EnvironmentSectionName).Bind(settings);
            configuration.Bind(settings);

            return settings;
        }

        /// <summary>
        /// Reads the <c>KEY=VALUE</c> lines of an environment file.
        /// </summary>
        /// <param name="path">The environment file.</param>
        /// <returns>The values by key, compared without case; empty when the file does not exist.</returns>
        /// <remarks>
        /// Blank lines and lines starting with <c>#</c> are skipped, and an <c>export</c> prefix is ignored. Matching single
        /// or double quotes around a value are removed; nothing else is unescaped or expanded. A repeated key keeps its
        /// last value.
        /// </remarks>
        /// <exception cref="FormatException">A line is not a <c>KEY=VALUE</c> pair. The message names the line only.</exception>
        public static IDictionary<string, string> ReadEnvironmentFile(string path)
        {
            // Require the path; a missing file is a valid, empty source.
            ArgumentException.ThrowIfNullOrWhiteSpace(argument: path);

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
            {
                return values;
            }

            // Parse each line; the line number identifies a malformed line without echoing its value.
            var lines = File.ReadAllLines(path);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                // Drop a shell export prefix, so a file shared with a shell script reads the same.
                if (line.StartsWith(ExportPrefix, StringComparison.Ordinal))
                {
                    line = line[ExportPrefix.Length..].TrimStart();
                }

                var separator = line.IndexOf('=');
                var key = separator > 0 ? line[..separator].Trim() : string.Empty;
                if (key.Length == 0)
                {
                    var message = $"Line {index + 1} of '{path}' is not a KEY=VALUE pair.";
                    throw new FormatException(message);
                }

                // Keep the value after the first separator, so values may contain '='; a repeated key overwrites.
                values[key] = RemoveQuotes(line[(separator + 1)..].Trim());
            }

            return values;
        }

        // Maps variable names to configuration keys and rewrites the 1 and 0 of switches, which the binder reads only as
        // true and false.
        private static Dictionary<string, string> ConvertToConfigurationValues(IDictionary<string, string> variables)
        {
            // Collect the keys of the boolean settings; only their values are rewritten.
            var switchKeys = typeof(EnvironmentSettings)
                .GetProperties()
                .Where(i => i.PropertyType == typeof(bool))
                .Select(i => i.GetCustomAttribute<ConfigurationKeyNameAttribute>()?.Name ?? i.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Translate each variable; a double underscore separates sections, as for the shell environment.
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in variables)
            {
                var key = name.Replace(KeyDelimiter, ConfigurationPath.KeyDelimiter, StringComparison.Ordinal);
                var isSwitch = switchKeys.Contains(ConfigurationPath.GetSectionKey(key));
                values[key] = (isSwitch, value?.Trim()) switch
                {
                    (true, "1") => bool.TrueString,
                    (true, "0") => bool.FalseString,
                    _ => value
                };
            }

            return values;
        }

        // Removes one pair of matching single or double quotes around a value.
        private static string RemoveQuotes(string value)
        {
            // Keep values that are not wrapped in the same quote on both ends, including a lone quote.
            var isQuoted = value.Length >= 2
                && (value[0] == '"' || value[0] == '\'')
                && value[^1] == value[0];

            return isQuoted ? value[1..^1] : value;
        }
        #endregion
    }
}
