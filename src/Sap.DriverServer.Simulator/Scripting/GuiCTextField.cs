using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiCTextField</c>: a text field with an input help (<c>ctxt</c>).
    /// </summary>
    /// <remarks>
    /// Fields with an input help open it as a hit list popup (<c>wnd[1]</c>) when the window receives F4 (virtual key
    /// 4) while the field has the focus, as in <c>setFocus</c> followed by <c>sendVKey 4</c>.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiCTextField : GuiTextField
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiCTextField"/> class.
        /// </summary>
        /// <param name="element">The text box.</param>
        public GuiCTextField(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
