using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiMenu</c>: a menu or menu entry (<c>menu</c>).
    /// </summary>
    /// <remarks>
    /// <c>select</c> runs the entry's function without opening the menu, as SAP GUI Scripting does.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiMenu : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiMenu"/> class.
        /// </summary>
        /// <param name="element">The menu item.</param>
        public GuiMenu(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Selects the menu entry.
        /// </summary>
        public void Select()
        {
            // Raise the menu item's click, which runs its function without opening the menu.
            Element.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        #endregion
    }
}
