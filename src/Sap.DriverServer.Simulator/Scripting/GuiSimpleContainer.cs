using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiSimpleContainer</c>: a subscreen container without scroll bars (<c>sub</c>).
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiSimpleContainer : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiSimpleContainer"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        public GuiSimpleContainer(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
