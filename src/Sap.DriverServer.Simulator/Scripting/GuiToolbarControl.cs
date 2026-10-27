using System.Runtime.InteropServices;
using System.Windows;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's toolbar control (<c>GuiShell</c> with <c>SubType</c> <c>Toolbar</c>).
    /// </summary>
    /// <remarks>
    /// Buttons are read by position and pressed by function code. Menu buttons need <c>pressContextButton</c> and then
    /// <c>selectContextMenuItem</c>; disabled buttons refuse <c>pressButton</c>. See <see cref="ShellToolbar"/>.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiToolbarControl : GuiShell
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiToolbarControl"/> class.
        /// </summary>
        /// <param name="element">The toolbar.</param>
        public GuiToolbarControl(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the number of buttons.
        /// </summary>
        public int ButtonCount => Toolbar.Buttons.Count;

        /// <inheritdoc />
        public override string SubType => "Toolbar";

        // Gets the wrapped toolbar.
        private ShellToolbar Toolbar => (ShellToolbar)Element;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets a value indicating whether a check button is checked.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns>True when the button is a checked check button.</returns>
        public bool GetButtonChecked(int position)
        {
            return Toolbar.GetButton(position).IsChecked;
        }

        /// <summary>
        /// Gets a value indicating whether a button can be pressed.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns>True when the button is enabled.</returns>
        public bool GetButtonEnabled(int position)
        {
            return Toolbar.GetButton(position).IsEnabled;
        }

        /// <summary>
        /// Gets the function code of a button.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns>The function code.</returns>
        public string GetButtonId(int position)
        {
            return Toolbar.GetButton(position).Id;
        }

        /// <summary>
        /// Gets the text of a button.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns>The button text.</returns>
        public string GetButtonText(int position)
        {
            return Toolbar.GetButton(position).Text;
        }

        /// <summary>
        /// Gets the tooltip of a button.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns>The tooltip.</returns>
        public string GetButtonTooltip(int position)
        {
            return Toolbar.GetButton(position).Tooltip;
        }

        /// <summary>
        /// Gets the type of a button: <c>Button</c>, <c>ButtonAndMenu</c>, <c>Menu</c>, or <c>CheckBox</c>.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns>The button type.</returns>
        public string GetButtonType(int position)
        {
            return Toolbar.GetButton(position).Type;
        }

        /// <summary>
        /// Presses a button, which runs its function or toggles a check button.
        /// </summary>
        /// <param name="buttonId">The button's function code.</param>
        public void PressButton(string buttonId)
        {
            // Run the button; disabled and menu-only buttons refuse the call.
            Toolbar.PressButton(buttonId);
        }

        /// <summary>
        /// Presses the menu part of a button, which opens its menu.
        /// </summary>
        /// <param name="buttonId">The button's function code.</param>
        public void PressContextButton(string buttonId)
        {
            // Open the menu; an item is chosen with selectContextMenuItem.
            Toolbar.PressContextButton(buttonId);
        }

        /// <summary>
        /// Selects an item of the open menu.
        /// </summary>
        /// <param name="functionCode">The item's function code.</param>
        public void SelectContextMenuItem(string functionCode)
        {
            // Run the item and close the menu; without an open menu the call fails.
            Toolbar.SelectMenuItem(functionCode);
        }
        #endregion
    }
}
