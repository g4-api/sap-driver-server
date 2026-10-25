using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Sap.DriverServer.Inspector.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Reads and saves the named driver session settings shown in the Create session dialog.
    /// </summary>
    /// <remarks>
    /// The sets live in one JSON file, by default <c>%APPDATA%\G4\SapInspector\capability-sets.json</c>. Saving writes a
    /// temporary file and moves it over the old one, so an interrupted save never leaves a truncated file. A missing
    /// or unreadable file reads as "no saved sets". Capability sets hold selection criteria only, never credentials.
    /// </remarks>
    /// <param name="filePath">The JSON file path.</param>
    public sealed class CapabilitySetStore(string filePath)
    {
        #region *** Constants    ***
        // Writes the file indented, because users may read or edit it.
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
        #endregion

        #region *** Fields       ***
        // Holds the JSON file path; its folder is created on the first save.
        private readonly string _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("A file path is required.", nameof(filePath))
            : filePath;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="CapabilitySetStore"/> class at the default location in the roaming profile.
        /// </summary>
        public CapabilitySetStore()
            : this(filePath: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "G4", "SapInspector", "capability-sets.json"))
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the JSON file path.
        /// </summary>
        public string FilePath => _filePath;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Reads the saved sets.
        /// </summary>
        /// <returns>The sets sorted by name; empty when the file is missing or unreadable.</returns>
        public IList<CapabilitySet> ReadSets()
        {
            // Treat a missing file as an empty list; the first save creates it.
            if (!File.Exists(_filePath))
            {
                return [];
            }

            try
            {
                // Read the list and drop entries without a name, which the dialog cannot show.
                var sets = JsonSerializer.Deserialize<List<CapabilitySet>>(File.ReadAllText(_filePath), JsonOptions) ?? [];
                return [.. sets.Where(i => !string.IsNullOrWhiteSpace(i?.Name)).OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)];
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // An unreadable file must not block creating sessions.
                return [];
            }
        }

        /// <summary>
        /// Saves the sets, replacing the file.
        /// </summary>
        /// <param name="sets">The sets to save.</param>
        public void SaveSets(IEnumerable<CapabilitySet> sets)
        {
            // Require the list; a null list has nothing to save and is a caller error.
            ArgumentNullException.ThrowIfNull(argument: sets);

            // Write a temporary file next to the target, then move it over the target.
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var temporaryPath = $"{_filePath}.tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(sets.ToList(), JsonOptions));
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        #endregion
    }
}
