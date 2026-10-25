using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiRadioButton</c>: one option of a group (<c>rad</c>).
    /// </summary>
    /// <remarks>
    /// Selecting a button clears the other buttons of its group.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiRadioButton : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiRadioButton"/> class.
        /// </summary>
        /// <param name="element">The radio button.</param>
        public GuiRadioButton(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the button is the group's selected option.
        /// </summary>
        public bool Selected => ((ToggleButton)Element).IsChecked == true;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Selects the button.
        /// </summary>
        public void Select()
        {
            // Check the button; WPF clears the other buttons of its group.
            ((ToggleButton)Element).IsChecked = true;
        }
        #endregion
    }
}
