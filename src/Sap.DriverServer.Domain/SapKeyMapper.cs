/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#keyboard-actions
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Converts W3C "Perform Actions" payloads into the SAP virtual keys accepted by <c>GuiFrameWindow.sendVKey</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SAP GUI Scripting cannot inject keystrokes; it can only raise function-key events through virtual keys.
    /// The mapper therefore accepts Enter, Escape (F12, Cancel), F1-F12 with Shift, Ctrl, or Ctrl+Shift, and the
    /// paging keys. Character keys, Alt, Tab, arrows, and every pointer or wheel action are rejected with
    /// <see cref="SapErrorKinds.NotExposedByScripting"/>; text belongs in the element "send keys" endpoint.
    /// </para>
    /// <para>
    /// The whole payload is validated before any key is sent, so an unsupported action never leaves SAP GUI in a
    /// half-applied state. The mapper is compute-only and fully unit-testable.
    /// </para>
    /// </remarks>
    public static class SapKeyMapper
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the virtual key value that marks a pause step rather than a key press.
        /// </summary>
        public const int PauseStep = -1;

        // Caps a single pause so a malformed payload cannot hold the request for minutes.
        private const int MaximumPauseMilliseconds = 10000;

        // Holds the W3C code points for Control (left and right), which add 24 to function keys.
        private static readonly HashSet<char> ControlKeys = ['', ''];

        // Maps unmodified W3C code points to SAP virtual keys: Enter, Return, Escape (= F12), and paging keys.
        private static readonly Dictionary<char, int> PlainKeys = new()
        {
            [''] = 0,
            [''] = 0,
            [''] = 12,
            [''] = 81,
            [''] = 82
        };

        // Maps Ctrl-modified paging keys: Ctrl+PageUp (first page) and Ctrl+PageDown (last page).
        private static readonly Dictionary<char, int> ControlPagingKeys = new()
        {
            [''] = 80,
            [''] = 83
        };

        // Holds the W3C code points for Shift (left and right), which add 12 to function keys.
        private static readonly HashSet<char> ShiftKeys = ['', ''];

        // Holds the W3C code points for Alt and Meta, which SAP virtual keys cannot express.
        private static readonly HashSet<char> UnsupportedModifierKeys = ['', '', '', ''];
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Converts an actions payload into an ordered list of virtual key and pause steps.
        /// </summary>
        /// <param name="actions">The W3C actions payload.</param>
        /// <returns>The steps in execution order; <see cref="KeyStep.VirtualKey"/> is <see cref="PauseStep"/> for pauses.</returns>
        /// <exception cref="SapDriverException">The payload contains an action with no SAP virtual key equivalent.</exception>
        public static IList<KeyStep> ConvertToSteps(ActionsModel actions)
        {
            // Treat an absent payload or source list as "nothing to do".
            var steps = new List<KeyStep>();
            var sources = actions?.Actions ?? [];

            foreach (var source in sources)
            {
                // Accept pause-only sources of any type; reject pointer and wheel input outright.
                var items = source.Actions ?? [];
                var isKeySource = string.Equals(source.Type, "key", StringComparison.OrdinalIgnoreCase);
                var isPauseOnly = items.All(i => string.Equals(GetValue(i, "type"), "pause", StringComparison.OrdinalIgnoreCase));

                if (!isKeySource && !isPauseOnly)
                {
                    throw SapDriverException.NewNotExposed($"'{source.Type}' input actions are not available through SAP GUI Scripting.");
                }

                // Convert the source's items in order, tracking modifier state across them.
                AddSourceSteps(steps, items);
            }

            return steps;
        }

        /// <summary>
        /// Converts one W3C key code point and the current modifiers into a SAP virtual key.
        /// </summary>
        /// <param name="key">The W3C key code point.</param>
        /// <param name="isShift">True when Shift is held.</param>
        /// <param name="isControl">True when Control is held.</param>
        /// <returns>The SAP virtual key.</returns>
        /// <exception cref="SapDriverException">The key and modifier combination has no SAP virtual key.</exception>
        public static int ConvertToVirtualKey(char key, bool isShift, bool isControl)
        {
            // Map F1-F12 with the SAP modifier offsets: none 0, Shift 12, Ctrl 24, Ctrl+Shift 36.
            var isFunctionKey = key is >= '' and <= '';
            if (isFunctionKey)
            {
                var offset = (isShift ? 12 : 0) + (isControl ? 24 : 0);
                return key - '' + 1 + offset;
            }

            // Map Ctrl+PageUp and Ctrl+PageDown to the first-page and last-page keys.
            if (isControl && !isShift && ControlPagingKeys.TryGetValue(key, out var pagingKey))
            {
                return pagingKey;
            }

            // Map the remaining keys only when no modifier is held.
            if (!isShift && !isControl && PlainKeys.TryGetValue(key, out var plainKey))
            {
                return plainKey;
            }

            // Reject every other combination; character input belongs in the element send keys endpoint.
            var message = $"Key U+{(int)key:X4} with the current modifiers has no SAP virtual key equivalent. " +
                "Use the element value endpoint to enter text.";
            throw SapDriverException.NewNotExposed(message);
        }

        // Converts the items of one input source into steps, tracking Shift and Ctrl state.
        // Appends to the caller-owned list; throws before appending when an item has no SAP equivalent.
        private static void AddSourceSteps(List<KeyStep> steps, IEnumerable<Dictionary<string, object>> items)
        {
            var isShift = false;
            var isControl = false;

            foreach (var item in items)
            {
                // Read the action type and its key code point, when any.
                var type = GetValue(item, "type");
                var value = GetValue(item, "value");
                var key = value.Length == 1 ? value[0] : '\0';

                switch (type)
                {
                    case "pause":
                        // Keep explicit pauses so timing-sensitive flows wait as requested.
                        var duration = int.TryParse(GetValue(item, "duration"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds)
                            ? Math.Clamp(milliseconds, 0, MaximumPauseMilliseconds)
                            : 0;
                        steps.Add(new KeyStep { PauseMilliseconds = duration, VirtualKey = PauseStep });
                        break;
                    case "keyDown":
                        // Record modifiers; any other key produces one virtual key with the current modifiers.
                        if (ShiftKeys.Contains(key))
                        {
                            isShift = true;
                        }
                        else if (ControlKeys.Contains(key))
                        {
                            isControl = true;
                        }
                        else if (UnsupportedModifierKeys.Contains(key) || value.Length != 1)
                        {
                            throw SapDriverException.NewNotExposed($"Key '{value}' has no SAP virtual key equivalent.");
                        }
                        else
                        {
                            steps.Add(new KeyStep { VirtualKey = ConvertToVirtualKey(key, isShift, isControl) });
                        }

                        break;
                    case "keyUp":
                        // Release modifiers; other key releases need no SAP call.
                        isShift = isShift && !ShiftKeys.Contains(key);
                        isControl = isControl && !ControlKeys.Contains(key);
                        break;
                    default:
                        throw SapDriverException.NewNotExposed($"The '{type}' key action is not available through SAP GUI Scripting.");
                }
            }
        }

        // Reads one action member as an invariant string; JSON elements format as their raw value.
        private static string GetValue(Dictionary<string, object> item, string name)
        {
            // Treat absent members as empty.
            return item is not null && item.TryGetValue(name, out var value) && value is not null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : string.Empty;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents one executable step: a virtual key press or a pause.
        /// </summary>
        public class KeyStep
        {
            /// <summary>
            /// Gets or sets the pause length in milliseconds; used only when <see cref="VirtualKey"/> is <see cref="PauseStep"/>.
            /// </summary>
            public int PauseMilliseconds { get; set; }

            /// <summary>
            /// Gets or sets the SAP virtual key, or <see cref="PauseStep"/> for a pause.
            /// </summary>
            public int VirtualKey { get; set; }
        }
        #endregion
    }
}
