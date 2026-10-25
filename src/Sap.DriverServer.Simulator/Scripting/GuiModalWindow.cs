using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiModalWindow</c>: a popup window (<c>wnd[1]</c>) that blocks the main window while it is open.
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiModalWindow : GuiFrameWindow
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiModalWindow"/> class.
        /// </summary>
        /// <param name="frame">The simulator window.</param>
        public GuiModalWindow(FrameWindow frame)
            : base(frame)
        { }
        #endregion
    }
}
