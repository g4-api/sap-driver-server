using System.Runtime.InteropServices;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiUserArea</c>: the user area (<c>usr</c>) that holds the current screen's fields.
    /// </summary>
    [ComVisible(true)]
    public sealed class GuiUserArea : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiUserArea"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        public GuiUserArea(FrameworkElement element)
            : base(element)
        { }
        #endregion
    }
}
