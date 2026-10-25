using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiLabel</c>: a label (<c>lbl</c>), whose text is read-only.
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiLabel : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiLabel"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        public GuiLabel(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
