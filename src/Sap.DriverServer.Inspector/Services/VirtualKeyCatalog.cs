using System.Collections.Generic;
using System.Globalization;

using Sap.DriverServer.Inspector.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Provides the standard SAP GUI virtual keys for sources that cannot ask SAP GUI for its own key descriptions.
    /// </summary>
    /// <remarks>
    /// Local inspection reads screen-specific descriptions through <c>GetVKeyDescription</c>; the driver has no such
    /// endpoint, so driver sessions offer this fixed list: Enter, the function keys with their Shift, Ctrl, and
    /// Ctrl+Shift variants, and the four paging keys. The same codes are what <c>sendVKey</c> accepts.
    /// </remarks>
    public static class VirtualKeyCatalog
    {
        #region *** Constants    ***
        // Holds the function key count per modifier group (F1 to F12).
        private const int FunctionKeyCount = 12;

        // Names the modifier prefixes of the four function key groups, in virtual key order.
        private static readonly string[] ModifierPrefixes = [string.Empty, "Shift+", "Ctrl+", "Ctrl+Shift+"];

        // Names the paging keys 80 to 83, in virtual key order.
        private static readonly string[] PagingKeys = ["Ctrl+Page Up (first page)", "Page Up", "Page Down", "Ctrl+Page Down (last page)"];
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the standard virtual keys.
        /// </summary>
        /// <returns>Enter (0), F1 to Ctrl+Shift+F12 (1 to 48), and the paging keys (80 to 83).</returns>
        public static IList<VirtualKeyEntry> GetStandardKeys()
        {
            // Start with Enter, the key every screen accepts.
            var keys = new List<VirtualKeyEntry> { new(0, "Enter") };

            // Number the function keys group by group: 1-12 plain, 13-24 Shift, 25-36 Ctrl, 37-48 Ctrl+Shift.
            for (var group = 0; group < ModifierPrefixes.Length; group++)
            {
                for (var index = 1; index <= FunctionKeyCount; index++)
                {
                    var key = (group * FunctionKeyCount) + index;
                    var name = string.Create(CultureInfo.InvariantCulture, $"{ModifierPrefixes[group]}F{index}");
                    keys.Add(new VirtualKeyEntry(key, name));
                }
            }

            // Add the paging keys, which follow a gap in the numbering.
            for (var index = 0; index < PagingKeys.Length; index++)
            {
                keys.Add(new VirtualKeyEntry(80 + index, PagingKeys[index]));
            }

            return keys;
        }
        #endregion
    }
}
