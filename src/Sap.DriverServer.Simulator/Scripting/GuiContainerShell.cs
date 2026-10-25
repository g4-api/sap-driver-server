using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiContainerShell</c>: a shell container (<c>shellcont</c>) that hosts a grid or tree shell.
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiContainerShell : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiContainerShell"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        public GuiContainerShell(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
