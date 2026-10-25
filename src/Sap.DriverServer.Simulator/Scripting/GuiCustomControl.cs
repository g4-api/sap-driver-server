using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiCustomControl</c>: a custom control area (<c>cntl</c>) that hosts a shell container.
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiCustomControl : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiCustomControl"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        public GuiCustomControl(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
