using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiTitlebar</c>: the title line under the system toolbar (<c>titl</c>).
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiTitlebar : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTitlebar"/> class.
        /// </summary>
        /// <param name="element">The title bar element.</param>
        public GuiTitlebar(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override string GetText()
        {
            return Frame.ScreenTitle;
        }
        #endregion
    }
}
