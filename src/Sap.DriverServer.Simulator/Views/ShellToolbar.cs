using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows a toolbar control (<c>GuiToolbarControl</c>): a shell with buttons, check buttons, and menu buttons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Buttons are addressed by function code. The types follow SAP's toolbar control: <c>Button</c> runs its function
    /// with <c>pressButton</c>; <c>ButtonAndMenu</c> runs its default function with <c>pressButton</c> and opens its menu
    /// with <c>pressContextButton</c>; <c>Menu</c> only opens its menu; <c>CheckBox</c> toggles its checked state.
    /// </para>
    /// <para>
    /// A disabled button refuses <c>pressButton</c>. A menu stays open for scripting until an item is selected, even
    /// when the visible popup closes. Functions are handed to <see cref="FunctionHandler"/>.
    /// </para>
    /// </remarks>
    public sealed class ShellToolbar : StackPanel
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies a button with a default function and a menu.
        /// </summary>
        public const string ButtonAndMenuType = "ButtonAndMenu";

        /// <summary>
        /// Specifies a plain button.
        /// </summary>
        public const string ButtonType = "Button";

        /// <summary>
        /// Specifies a check button that toggles its checked state.
        /// </summary>
        public const string CheckBoxType = "CheckBox";

        /// <summary>
        /// Specifies a button that only opens a menu.
        /// </summary>
        public const string MenuType = "Menu";
        #endregion

        #region *** Fields       ***
        // Holds the buttons in display order.
        private IReadOnlyList<ButtonEntry> _buttons = [];

        // Holds the items of the open menu; null when no menu is open.
        private IReadOnlyList<AlvGrid.MenuEntry> _openMenu;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ShellToolbar"/> class.
        /// </summary>
        public ShellToolbar()
        {
            Orientation = Orientation.Horizontal;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the buttons in display order.
        /// </summary>
        public IReadOnlyList<ButtonEntry> Buttons => _buttons;

        /// <summary>
        /// Gets or sets the handler that runs a function code from a button or a menu item.
        /// </summary>
        public Action<string> FunctionHandler { get; set; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets a button by position.
        /// </summary>
        /// <param name="position">The zero-based position.</param>
        /// <returns>The button.</returns>
        /// <exception cref="SimulatorException">The position is outside the toolbar.</exception>
        public ButtonEntry GetButton(int position)
        {
            // Reject positions outside the toolbar with a message that names the position.
            if (position < 0 || position >= _buttons.Count)
            {
                throw new SimulatorException($"The toolbar has no button at position {position}.");
            }

            return _buttons[position];
        }

        /// <summary>
        /// Presses a button: runs its function, or toggles a check button.
        /// </summary>
        /// <param name="buttonId">The button's function code.</param>
        /// <exception cref="SimulatorException">The button does not exist, is disabled, or only opens a menu.</exception>
        public void PressButton(string buttonId)
        {
            // Require an enabled button that has a function of its own.
            var button = GetEnabledButton(buttonId);
            if (button.Type == MenuType)
            {
                throw new SimulatorException($"The button '{buttonId}' only opens a menu; press it with pressContextButton.");
            }

            // Toggle check buttons first, so the function sees the new state; then show the state and run the function.
            if (button.Type == CheckBoxType)
            {
                button.IsChecked = !button.IsChecked;
                ShowButtons();
            }

            FunctionHandler?.Invoke(button.Id);
        }

        /// <summary>
        /// Presses the menu part of a button, which opens its menu.
        /// </summary>
        /// <param name="buttonId">The button's function code.</param>
        /// <exception cref="SimulatorException">The button does not exist, is disabled, or has no menu.</exception>
        public void PressContextButton(string buttonId)
        {
            // Require an enabled button with a menu, then record the menu and show it at the toolbar.
            var button = GetEnabledButton(buttonId);
            if (button.Menu.Count == 0)
            {
                throw new SimulatorException($"The button '{buttonId}' has no menu; press it with pressButton.");
            }

            _openMenu = button.Menu;
            var menu = new ContextMenu { PlacementTarget = this };
            foreach (var item in button.Menu)
            {
                var menuItem = new MenuItem { Header = item.Text };
                menuItem.Click += (_, _) => SelectMenuItem(item.Id);
                menu.Items.Add(menuItem);
            }

            ContextMenu = menu;
            menu.IsOpen = true;
        }

        /// <summary>
        /// Selects an item of the open menu and closes the menu.
        /// </summary>
        /// <param name="functionCode">The item's function code.</param>
        /// <exception cref="SimulatorException">No menu is open, or the menu has no such item.</exception>
        public void SelectMenuItem(string functionCode)
        {
            // Require an open menu and a matching item; close the menu before the function runs.
            var menu = _openMenu ?? throw new SimulatorException("No context menu is open.");
            var item = menu.FirstOrDefault(i => string.Equals(i.Id, functionCode, StringComparison.Ordinal))
                ?? throw new SimulatorException($"The open context menu has no item '{functionCode}'.");
            _openMenu = null;
            if (ContextMenu is not null)
            {
                ContextMenu.IsOpen = false;
                ContextMenu = null;
            }

            FunctionHandler?.Invoke(item.Id);
        }

        /// <summary>
        /// Sets the buttons and draws them.
        /// </summary>
        /// <param name="buttons">The buttons in display order.</param>
        public void SetButtons(IReadOnlyList<ButtonEntry> buttons)
        {
            // Keep the entries, which also hold the checked state, and draw them.
            ArgumentNullException.ThrowIfNull(argument: buttons);

            _buttons = buttons;
            ShowButtons();
        }

        // Gets an enabled button by function code, raising an error that names the problem.
        private ButtonEntry GetEnabledButton(string buttonId)
        {
            // Find the button, then refuse disabled ones.
            var button = _buttons.FirstOrDefault(i => string.Equals(i.Id, buttonId, StringComparison.Ordinal))
                ?? throw new SimulatorException($"The toolbar has no button '{buttonId}'.");

            return button.IsEnabled ? button : throw new SimulatorException($"The button '{buttonId}' is disabled.");
        }

        // Runs a clicked button: menu buttons open their menu, other buttons run their function.
        private void OnButtonClick(ButtonEntry entry)
        {
            // Treat a click like the matching scripting call; refused presses do nothing visible.
            try
            {
                if (entry.Type == MenuType)
                {
                    PressContextButton(entry.Id);
                }
                else
                {
                    PressButton(entry.Id);
                }
            }
            catch (SimulatorException)
            {
                // A disabled or wrong-type press has no effect on screen, as with a real toolbar.
            }
        }

        // Draws the buttons; clicks go through the same methods as scripting.
        private void ShowButtons()
        {
            // Rebuild every button, so the checked state and the enabled state are current.
            Children.Clear();
            foreach (var entry in _buttons)
            {
                ButtonBase button = entry.Type == CheckBoxType
                    ? new ToggleButton { IsChecked = entry.IsChecked }
                    : new Button();
                button.Content = entry.Menu.Count > 0 ? entry.Text + " ▾" : entry.Text;
                button.IsEnabled = entry.IsEnabled;
                button.Margin = new Thickness(0, 0, 4, 0);
                button.Padding = new Thickness(8, 2, 8, 2);
                button.ToolTip = entry.Tooltip;
                button.Click += (_, _) => OnButtonClick(entry);
                Children.Add(button);
            }
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Describes one toolbar button: function code, text, type, state, and menu.
        /// </summary>
        public sealed class ButtonEntry
        {
            #region *** Properties   ***
            /// <summary>
            /// Gets the function code.
            /// </summary>
            public string Id { get; init; }

            /// <summary>
            /// Gets or sets a value indicating whether a check button is checked.
            /// </summary>
            public bool IsChecked { get; set; }

            /// <summary>
            /// Gets a value indicating whether the button can be pressed.
            /// </summary>
            public bool IsEnabled { get; init; } = true;

            /// <summary>
            /// Gets the menu items of a menu or button-and-menu button; empty for the other types.
            /// </summary>
            public IReadOnlyList<AlvGrid.MenuEntry> Menu { get; init; } = [];

            /// <summary>
            /// Gets the button text.
            /// </summary>
            public string Text { get; init; }

            /// <summary>
            /// Gets the tooltip.
            /// </summary>
            public string Tooltip { get; init; }

            /// <summary>
            /// Gets the type: one of the <see cref="ShellToolbar"/> type constants.
            /// </summary>
            public string Type { get; init; } = ButtonType;
            #endregion
        }
        #endregion
    }
}
