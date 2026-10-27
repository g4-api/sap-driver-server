using System.Runtime.InteropServices;
using System.Windows;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiStatusbar</c>: the message line of the main window (<c>sbar</c>).
    /// </summary>
    /// <remarks>
    /// Simulator messages use the message class <c>ZSIM</c> and the types <c>S</c>, <c>W</c>, <c>E</c>, <c>I</c>, and
    /// <c>A</c>. A message shown in a popup stays the status bar's message, with <c>MessageAsPopup</c> set.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiStatusbar : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiStatusbar"/> class.
        /// </summary>
        /// <param name="element">The status bar element.</param>
        public GuiStatusbar(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the current message is shown in a popup (<c>wnd[1]</c>).
        /// </summary>
        public bool MessageAsPopup => Message.IsPopup;

        /// <summary>
        /// Gets the message class, such as <c>ZSIM</c>; empty when no message is shown.
        /// </summary>
        public string MessageId => Message.MessageId;

        /// <summary>
        /// Gets the three-digit message number; empty when no message is shown.
        /// </summary>
        public string MessageNumber => Message.MessageNumber;

        /// <summary>
        /// Gets the message type: <c>S</c>, <c>W</c>, <c>E</c>, <c>I</c>, <c>A</c>, or empty.
        /// </summary>
        public string MessageType => Message.MessageType;

        // Gets the session's current message.
        private StatusMessage Message => Frame.Session.Status;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override string GetText()
        {
            return Message.Text;
        }
        #endregion
    }
}
