using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiShell</c>: an ActiveX control (<c>shell</c>) whose kind is named by <see cref="SubType"/>.
    /// </summary>
    /// <remarks>
    /// Every shell reports the type <c>GuiShell</c>, as SAP GUI does; derived classes add the members of their kind.
    /// </remarks>
    [ComVisible(true)]
    public abstract class GuiShell : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiShell"/> class.
        /// </summary>
        /// <param name="element">The WPF control.</param>
        protected GuiShell(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the shell kind, such as <c>GridView</c> or <c>Tree</c>.
        /// </summary>
        public abstract string SubType { get; }

        /// <inheritdoc />
        public override string Type => nameof(GuiShell);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Raises a double click on the control, as if the user double-clicked its current item.
        /// </summary>
        protected void RaiseDoubleClick()
        {
            // Raise the same routed event a mouse double click produces, so the screen's handler runs.
            var arguments = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = Control.MouseDoubleClickEvent
            };
            Element.RaiseEvent(arguments);
        }
        #endregion
    }
}
