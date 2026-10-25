using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiCTextField</c>: a text field with an input help (<c>ctxt</c>).
    /// </summary>
    /// <remarks>
    /// The simulator does not open an input help; the type exists so locators and type-specific behavior match SAP GUI.
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
