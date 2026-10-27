using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiBox</c>: the frame and title of a group of fields (<c>box</c>).
    /// </summary>
    /// <remarks>
    /// A box only draws a frame. The fields inside it on screen are its siblings in the scripting tree, not its
    /// children, so a locator cannot reach them through the box. The text is the box title.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiBox : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiBox"/> class.
        /// </summary>
        /// <param name="element">The group box.</param>
        public GuiBox(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
