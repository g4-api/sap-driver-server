using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiButton</c>: a push button (<c>btn</c>) in a toolbar or on a screen.
    /// </summary>
    /// <remarks>
    /// <c>press</c> raises the button's click, so the same handler runs as for a mouse click. Toolbar buttons send their
    /// virtual key; a key the session refuses produces a beep, not a scripting error, as for a mouse click.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiButton : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiButton"/> class.
        /// </summary>
        /// <param name="element">The button.</param>
        public GuiButton(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Presses the button.
        /// </summary>
        /// <exception cref="SimulatorException">The button is disabled.</exception>
        public void Press()
        {
            // Refuse disabled buttons; SAP GUI does not run their function either.
            if (!Element.IsEnabled)
            {
                throw new SimulatorException($"The button '{Id}' is disabled.");
            }

            Element.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }
        #endregion
    }
}
