using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiPasswordField</c>: a masked input field (<c>pwd</c>).
    /// </summary>
    /// <remarks>
    /// Writing <c>Text</c> passes the value straight to the password box; reading <c>Text</c> always returns an empty
    /// string, so the simulator never hands a password back to a client.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiPasswordField : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiPasswordField"/> class.
        /// </summary>
        /// <param name="element">The password box.</param>
        public GuiPasswordField(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the maximum number of characters; zero when unlimited.
        /// </summary>
        public int MaxLength => PasswordBox.MaxLength;

        // Gets the wrapped password box.
        private PasswordBox PasswordBox => (PasswordBox)Element;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override string GetText()
        {
            // Never return the password.
            return string.Empty;
        }

        /// <inheritdoc />
        protected override void SetText(string value)
        {
            // Pass the value straight to the password box; nothing else keeps it.
            PasswordBox.Password = value ?? string.Empty;
        }
        #endregion
    }
}
