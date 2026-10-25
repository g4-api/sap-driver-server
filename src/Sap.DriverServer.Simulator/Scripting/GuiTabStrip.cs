using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiTabStrip</c>: a set of tabs (<c>tabs</c>).
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiTabStrip : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTabStrip"/> class.
        /// </summary>
        /// <param name="element">The tab control.</param>
        public GuiTabStrip(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the selected tab; null when no tab is selected.
        /// </summary>
        public object SelectedTab => ((TabControl)Element).SelectedItem is TabItem tab ? new GuiTab(tab) : null;
        #endregion
    }
}
