using System;
using System.Collections.Generic;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Lists and performs the quick actions the details pane offers for a control.
    /// </summary>
    /// <remarks>
    /// Quick actions are exactly the interactions the driver performs, through <see cref="SapControlInteractions"/>,
    /// so trying an action in the inspector predicts what a WebDriver click or send keys does. Every action runs only
    /// when the user presses its button.
    /// </remarks>
    public static class ActionCatalog
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the driver's click equivalent (press, select, toggle, or set focus, by control type).
        /// </summary>
        public const string Click = "click";

        /// <summary>
        /// Specifies clearing the control's text.
        /// </summary>
        public const string Clear = "clear";

        /// <summary>
        /// Specifies setting the keyboard focus.
        /// </summary>
        public const string Focus = "focus";

        /// <summary>
        /// Specifies replacing the control's text, or selecting a combo box entry by key.
        /// </summary>
        public const string SetText = "setText";

        // Lists the control types whose text can be written and cleared.
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
        /// Gets the quick actions available for a control type.
        /// </summary>
        /// <param name="type">The SAP type.</param>
        /// <param name="subType">The shell subtype, or empty.</param>
        /// <returns>The actions in button order; empty for objects above window level.</returns>
        public static IList<QuickAction> GetActions(string type, string subType)
        {
            // Offer nothing for the application, connections, and sessions, which are not visual controls.
            var actions = new List<QuickAction>();
            var isAboveWindow = type is "GuiApplication" or "GuiConnection" or SapGuiTypes.Session;
            if (string.IsNullOrEmpty(type) || isAboveWindow)
            {
                return actions;
            }

            // Offer the click equivalent under the label matching what it does for this control family.
            var clickLabel = GetClickLabel(type);
            if (clickLabel is not null)
            {
                actions.Add(new QuickAction(Click, clickLabel, RequiresText: false));
            }

            // Offer focus for every visual control, and text actions where the driver supports them.
            actions.Add(new QuickAction(Focus, "Set focus", RequiresText: false));

            var isTextEdit = type == SapGuiTypes.Shell && subType == SapGuiTypes.TextEditSubType;
            if (TextTypes.Contains(type) || isTextEdit)
            {
                actions.Add(new QuickAction(SetText, "Set text", RequiresText: true));
                actions.Add(new QuickAction(Clear, "Clear", RequiresText: false));
            }
            else if (type == SapGuiTypes.ComboBox)
            {
                actions.Add(new QuickAction(SetText, "Select key", RequiresText: true));
            }

            return actions;
        }

        /// <summary>
        /// Performs a quick action on a control.
        /// </summary>
        /// <param name="node">The control node, inside a scripting operation.</param>
        /// <param name="actionId">The action identifier.</param>
        /// <param name="text">The action text; used by <see cref="SetText"/> only.</param>
        /// <exception cref="SapDriverException">The control does not support the action.</exception>
        public static void InvokeAction(ISapGuiNode node, string actionId, string text)
        {
            // Dispatch to the same members the driver uses, so behavior and failures match WebDriver calls.
            switch (actionId)
            {
                case Click:
                    SapControlInteractions.InvokeClick(node);
                    break;
                case Focus:
                    node.InvokeMethod("setFocus");
                    break;
                case SetText:
                    SapControlInteractions.SetText(node, text);
                    break;
                case Clear:
                    SapControlInteractions.ClearText(node);
                    break;
                default:
                    throw new ArgumentException($"Unknown quick action '{actionId}'.", nameof(actionId));
            }
        }

        // Gets the click button label for a control family, or null when the driver has no click for it.
        private static string GetClickLabel(string type)
        {
            // Mirror SapControlInteractions.InvokeClick, which owns the actual mapping.
            return type switch
            {
                SapGuiTypes.Button => "Press",
                SapGuiTypes.Menu or SapGuiTypes.RadioButton or SapGuiTypes.Tab => "Select",
                SapGuiTypes.CheckBox => "Toggle",
                _ => null
            };
        }
        #endregion
    }
}
