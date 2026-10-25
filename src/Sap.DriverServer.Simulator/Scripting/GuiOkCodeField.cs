using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiOkCodeField</c>: the command field in the system toolbar (<c>okcd</c>).
    /// </summary>
    /// <remarks>
    /// Typing a transaction such as <c>/nFORM</c> and sending Enter (virtual key 0) starts it.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiOkCodeField : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiOkCodeField"/> class.
        /// </summary>
        /// <param name="element">The command text box.</param>
        public GuiOkCodeField(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the command field is expanded; the simulator always shows it.
        /// </summary>
        public bool Opened => Element.IsVisible;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override void SetText(string value)
        {
            // Write the OK code; it runs when Enter is sent.
            ((TextBox)Element).Text = value ?? string.Empty;
        }
        #endregion
    }
}
