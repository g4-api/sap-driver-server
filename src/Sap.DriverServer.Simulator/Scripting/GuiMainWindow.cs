using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiMainWindow</c>: the main window of a session (<c>wnd[0]</c>).
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiMainWindow : GuiFrameWindow
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiMainWindow"/> class.
        /// </summary>
        /// <param name="frame">The simulator window.</param>
        public GuiMainWindow(FrameWindow frame)
            : base(frame)
        { }
        #endregion
    }
}
