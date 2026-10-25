using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiCheckBox</c>: a check box (<c>chk</c>).
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiCheckBox : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiCheckBox"/> class.
        /// </summary>
        /// <param name="element">The check box.</param>
        public GuiCheckBox(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets a value indicating whether the box is checked.
        /// </summary>
        public bool Selected
        {
            get => ((ToggleButton)Element).IsChecked == true;
            set => ((ToggleButton)Element).IsChecked = value;
        }
        #endregion
    }
}
