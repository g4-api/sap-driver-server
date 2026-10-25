using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiToolbar</c>: a toolbar: the system toolbar (<c>tbar[0]</c>) with the command field, or the application toolbar (<c>tbar[1]</c>).
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiToolbar : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiToolbar"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        public GuiToolbar(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
