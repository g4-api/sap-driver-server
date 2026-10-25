using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiMenubar</c>: the menu bar (<c>mbar</c>).
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiMenubar : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiMenubar"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        public GuiMenubar(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
