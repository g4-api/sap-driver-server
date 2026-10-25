using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiSimpleContainer</c>: a subscreen container (<c>ssub</c>), such as the content of a tab.
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
