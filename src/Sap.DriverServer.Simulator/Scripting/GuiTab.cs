using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiTab</c>: one tab of a tab strip (<c>tabp</c>).
    /// </summary>
    /// <remarks>
    /// Only the selected tab has children: SAP GUI loads a tab's subscreen when the tab is selected.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiTab : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTab"/> class.
        /// </summary>
        /// <param name="element">The tab item.</param>
        public GuiTab(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Selects the tab, which shows its content.
        /// </summary>
        public void Select()
        {
            // Select the tab, which brings its content into the scripting tree.
            ((TabItem)Element).IsSelected = true;
        }
        #endregion
    }
}
