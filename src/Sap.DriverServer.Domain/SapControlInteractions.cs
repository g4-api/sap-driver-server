using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Maps the W3C element interactions (click, send keys, clear, text, attribute) to SAP GUI Scripting members by control type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SAP GUI Scripting has no generic "click" or keystroke injection; each control family exposes its own
    /// members. The mapping below uses only those members and reports every other combination explicitly:
    /// an interaction the control family cannot perform is <see cref="WebDriverErrorCodes.ElementNotInteractable"/>,
    /// and a control family scripting does not drive generically (shells) is <see cref="WebDriverErrorCodes.UnsupportedOperation"/>.
    /// </para>
    /// <para>
    /// Send keys replaces the control's text, because SAP GUI Scripting sets the <c>text</c> property rather than
    /// typing. Password field text is written but never read back.
    /// </para>
    /// </remarks>
    public static class SapControlInteractions
    {
        #region *** Constants    ***
        // Maps public attribute names (case-insensitive) to the SAP GUI Scripting properties they read.
        private static readonly Dictionary<string, string> AttributeProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            ["changeable"] = "Changeable",
            ["containerType"] = "ContainerType",
            ["defaultTooltip"] = "DefaultTooltip",
            ["handle"] = "Handle",
            ["height"] = "Height",
            ["highlighted"] = "Highlighted",
            ["iconName"] = "IconName",
            ["id"] = "Id",
            ["key"] = "Key",
            ["left"] = "ScreenLeft",
            ["maxLength"] = "MaxLength",
            ["modified"] = "Modified",
            ["name"] = "Name",
            ["numerical"] = "Numerical",
            ["required"] = "Required",
            ["selected"] = "Selected",
            ["subType"] = "SubType",
            ["text"] = "Text",
            ["tooltip"] = "Tooltip",
            ["top"] = "ScreenTop",
            ["type"] = "Type",
            ["value"] = "Value",
            ["width"] = "Width"
        };

        // Lists the control types whose click is a focus change, matching what a mouse click does in SAP GUI.
        private static readonly HashSet<string> FocusTypes = new(StringComparer.Ordinal)
        {
            SapGuiTypes.ComboBox,
            SapGuiTypes.ContextTextField,
            SapGuiTypes.Label,
            SapGuiTypes.OkCodeField,
            SapGuiTypes.PasswordField,
            SapGuiTypes.TextField
        };

        // Lists the control types whose click invokes the select method.
        private static readonly HashSet<string> SelectTypes = new(StringComparer.Ordinal)
        {
            SapGuiTypes.Menu,
            SapGuiTypes.RadioButton,
            SapGuiTypes.Tab
        };

        // Lists the single-line control types whose text property is writable.
        private static readonly HashSet<string> TextTypes = new(StringComparer.Ordinal)
        {
            SapGuiTypes.ContextTextField,
            SapGuiTypes.OkCodeField,
            SapGuiTypes.PasswordField,
            SapGuiTypes.TextField
        };
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Clears a text control by setting its text to an empty string.
        /// </summary>
        /// <param name="node">The control node.</param>
        /// <exception cref="SapDriverException">The control has no writable text.</exception>
        public static void ClearText(ISapGuiNode node)
        {
            // Reuse the text mapping; combo boxes have no "empty" key and are rejected there.
            var type = node.Type;
            var isComboBox = string.Equals(type, SapGuiTypes.ComboBox, StringComparison.Ordinal);
            if (isComboBox || !TestTextControl(node, type))
            {
                throw NewNotInteractableException(type, interaction: "clear");
            }

            node.SetProperty("Text", string.Empty);
        }

        /// <summary>
        /// Gets a whitelisted attribute of a control as a string.
        /// </summary>
        /// <param name="node">The control node.</param>
        /// <param name="name">The attribute name, matched case-insensitively against the whitelist.</param>
        /// <returns>The attribute value; an empty string when the name is unknown, not exposed, or a password text.</returns>
        public static string GetAttribute(ISapGuiNode node, string name)
        {
            // Unknown names follow the UIA contract and return an empty string instead of an error.
            if (string.IsNullOrWhiteSpace(name) || !AttributeProperties.TryGetValue(name.Trim(), out var propertyName))
            {
                return string.Empty;
            }

            // Never read password text, whichever attribute name requests it.
            var isPasswordText = propertyName == "Text" && string.Equals(node.Type, SapGuiTypes.PasswordField, StringComparison.Ordinal);
            if (isPasswordText)
            {
                return string.Empty;
            }

            // Format window handles in the same hexadecimal form as the window endpoints.
            return propertyName == "Handle"
                ? SapGuiNavigator.GetHandle(node)
                : SapGuiNavigator.GetString(node, propertyName);
        }

        /// <summary>
        /// Gets the visible text of a control.
        /// </summary>
        /// <param name="node">The control node.</param>
        /// <returns>The text; an empty string for password fields and controls without text.</returns>
        public static string GetText(ISapGuiNode node)
        {
            // Never read password text; return the same empty value as the UIA server.
            if (string.Equals(node.Type, SapGuiTypes.PasswordField, StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return SapGuiNavigator.GetString(node, propertyName: "Text");
        }

        /// <summary>
        /// Performs the SAP equivalent of a click on a control.
        /// </summary>
        /// <param name="node">The control node.</param>
        /// <exception cref="SapDriverException">The control type has no click equivalent in SAP GUI Scripting.</exception>
        public static void InvokeClick(ISapGuiNode node)
        {
            // Dispatch on the control family; each branch uses the member SAP documents for that family.
            var type = node.Type;

            if (string.Equals(type, SapGuiTypes.Button, StringComparison.Ordinal))
            {
                node.InvokeMethod("press");
                return;
            }

            if (SelectTypes.Contains(type))
            {
                node.InvokeMethod("select");
                return;
            }

            // Toggle check boxes, which is what a click does on screen.
            if (string.Equals(type, SapGuiTypes.CheckBox, StringComparison.Ordinal))
            {
                node.SetProperty("Selected", !SapGuiNavigator.GetBoolean(node, propertyName: "Selected"));
                return;
            }

            if (FocusTypes.Contains(type))
            {
                node.InvokeMethod("setFocus");
                return;
            }

            // Report shells and every other family explicitly; no coordinate click is attempted.
            var subType = string.Equals(type, SapGuiTypes.Shell, StringComparison.Ordinal)
                ? $" (subType '{SapGuiNavigator.GetString(node, propertyName: "SubType")}')"
                : string.Empty;
            var message = $"Click is not available through SAP GUI Scripting for '{type}'{subType}.";

            throw SapDriverException.NewNotExposed(message);
        }

        /// <summary>
        /// Writes text to a control, replacing its current content.
        /// </summary>
        /// <param name="node">The control node.</param>
        /// <param name="text">The text to write; for combo boxes, the key of the entry to select.</param>
        /// <exception cref="SapDriverException">The control has no writable text.</exception>
        public static void SetText(ISapGuiNode node, string text)
        {
            // Select combo box entries by key, which is the value SAP records in scripts.
            var type = node.Type;
            if (string.Equals(type, SapGuiTypes.ComboBox, StringComparison.Ordinal))
            {
                node.SetProperty("Key", text ?? string.Empty);
                return;
            }

            // Reject controls whose text is read-only.
            if (!TestTextControl(node, type))
            {
                throw NewNotInteractableException(type, interaction: "send keys");
            }

            node.SetProperty("Text", text ?? string.Empty);
        }

        // Builds the "element not interactable" failure for an interaction the control family cannot perform.
        private static SapDriverException NewNotInteractableException(string type, string interaction)
        {
            // Name the interaction and type only; the control's content is never quoted.
            var message = $"The '{interaction}' interaction is not available for SAP control type '{type}'.";

            return new SapDriverException(
                WebDriverErrorCodes.ElementNotInteractable,
                SapErrorKinds.NotExposedByScripting,
                StatusCodes.Status400BadRequest,
                message);
        }

        // Tests whether a control has writable text: single-line fields and text edit shells.
        private static bool TestTextControl(ISapGuiNode node, string type)
        {
            // Single-line fields are identified by type alone.
            if (TextTypes.Contains(type))
            {
                return true;
            }

            // Multi-line text edits are shells with the TextEdit subtype.
            return string.Equals(type, SapGuiTypes.Shell, StringComparison.Ordinal)
                && string.Equals(SapGuiNavigator.GetString(node, propertyName: "SubType"), SapGuiTypes.TextEditSubType, StringComparison.Ordinal);
        }
        #endregion
    }
}
