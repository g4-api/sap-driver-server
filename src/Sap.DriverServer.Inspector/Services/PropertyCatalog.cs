/*
 * RESOURCES
 * https://help.sap.com/docs/sap_gui_for_windows/b47d018c3b9b45e897faf66a6c0885a8/babdf65f4d0a4bd8b40f5ff132cb12fa.html
 */
using System;
using System.Collections.Generic;
using System.Globalization;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Reads the scalar SAP GUI Scripting properties shown in the details pane.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The catalog lists the common <c>GuiComponent</c>, <c>GuiVComponent</c>, field, window, and shell properties.
    /// Each object exposes only a subset, so every read is lenient: properties the object does not expose are
    /// skipped rather than reported as errors. Object-valued properties such as <c>Parent</c> are not listed.
    /// </para>
    /// <para>
    /// Password field text is never read; the row shows <see cref="MaskedValue"/> instead. Session information
    /// comes from <c>GuiSessionInfo</c> and deliberately omits the logged-on user.
    /// </para>
    /// </remarks>
    public static class PropertyCatalog
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the placeholder shown instead of password text.
        /// </summary>
        public const string MaskedValue = "(hidden)";

        // Lists the properties shown in the basic view, in display order.
        private static readonly string[] BasicProperties =
        [
            "Id", "Type", "Name", "Text", "Tooltip", "Changeable", "ScreenLeft", "ScreenTop", "Width", "Height"
        ];

        // Lists the additional properties shown in the "all" view, in display order.
        private static readonly string[] ExtendedProperties =
        [
            "SubType", "ContainerType", "DefaultTooltip", "IconName", "Modified", "Highlighted", "Required",
            "Numerical", "MaxLength", "CaretPosition", "DisplayedText", "Key", "Value", "Selected", "Left", "Top",
            "AccText", "AccTooltip", "Handle", "Iconic", "WorkingPaneWidth", "WorkingPaneHeight", "Busy",
            "MessageType", "MessageId", "MessageNumber", "Description", "DisabledByServer"
        ];

        // Lists the GuiSessionInfo properties shown for sessions; the user name is intentionally absent.
        private static readonly string[] SessionInfoProperties =
        [
            "SystemName", "Client", "Language", "Transaction", "Program", "ScreenNumber", "SessionNumber", "ApplicationServer"
        ];

        // Lists the properties whose value is password text on password fields.
        private static readonly HashSet<string> TextProperties = new(StringComparer.OrdinalIgnoreCase) { "Text", "DisplayedText", "Value" };
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Reads every catalog property the node exposes.
        /// </summary>
        /// <param name="node">The node to read, inside a scripting operation.</param>
        /// <param name="type">The node's SAP type, read once by the caller.</param>
        /// <returns>The exposed properties in catalog order; basic properties first.</returns>
        public static IList<PropertyEntry> GetProperties(ISapGuiNode node, string type)
        {
            // Read the basic set, then the extended set; both skip properties the object does not expose.
            var entries = new List<PropertyEntry>();
            var isPassword = string.Equals(type, SapGuiTypes.PasswordField, StringComparison.Ordinal);

            var basicEntries = ReadEntries(node, isPassword, BasicProperties);
            foreach (var entry in basicEntries)
            {
                entry.IsBasic = true;
            }

            entries.AddRange(basicEntries);
            entries.AddRange(ReadEntries(node, isPassword, ExtendedProperties));

            // Add session information through GuiSessionInfo, prefixed so it is distinguishable from session properties.
            if (string.Equals(type, SapGuiTypes.Session, StringComparison.Ordinal))
            {
                AddSessionInfo(entries, node);
            }

            return entries;
        }

        // Formats a scalar property value: Booleans in lowercase and window handles in hexadecimal, as the driver does.
        private static string ConvertToText(ISapGuiNode node, string name, object value)
        {
            // Reuse the driver's handle formatting so handles match the window endpoints.
            if (name == "Handle")
            {
                return SapGuiNavigator.GetHandle(node);
            }

            return value switch
            {
                null => string.Empty,
                bool isSet => isSet ? "true" : "false",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }

        // Reads one list of properties, masking password text and skipping unexposed or object-valued properties.
        // Each property is read once; the returned entries are not marked basic.
        private static List<PropertyEntry> ReadEntries(ISapGuiNode node, bool isPassword, string[] names)
        {
            var entries = new List<PropertyEntry>();

            foreach (var name in names)
            {
                // Never request password text from SAP GUI; show the placeholder instead.
                if (isPassword && TextProperties.Contains(name))
                {
                    entries.Add(new PropertyEntry { Name = name, Value = MaskedValue });
                    continue;
                }

                // Skip properties the object does not expose and object-valued properties.
                if (!SapGuiNavigator.TestProperty(node, name, out var value) || value is ISapGuiNode)
                {
                    continue;
                }

                entries.Add(new PropertyEntry { Name = name, Value = ConvertToText(node, name, value) });
            }

            return entries;
        }

        // Reads the GuiSessionInfo properties of a session into entries prefixed with "Info.".
        private static void AddSessionInfo(List<PropertyEntry> entries, ISapGuiNode session)
        {
            // Sessions that are busy or closing can lack Info; treat that as "no information".
            if (!SapGuiNavigator.TestProperty(session, "Info", out var value) || value is not ISapGuiNode info)
            {
                return;
            }

            foreach (var entry in ReadEntries(info, isPassword: false, SessionInfoProperties))
            {
                entry.IsBasic = true;
                entry.Name = $"Info.{entry.Name}";
                entries.Add(entry);
            }
        }
        #endregion
    }
}
