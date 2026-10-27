using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiUserArea</c>: the user area (<c>usr</c>) that holds the current screen's fields.
    /// </summary>
    /// <remarks>
    /// The user area is a scroll container. Screens that page their content, such as lists, implement
    /// <see cref="IScrollableScreen"/>; the user area's vertical scroll bar then moves their first visible line.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiUserArea : GuiScrollContainer
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

        #region *** Methods      ***
        /// <inheritdoc />
        protected override GuiScrollbar NewScrollbar(bool isVertical)
        {
            // Let a scrollable screen own the vertical position; everything else scrolls like a plain container.
            if (isVertical && (Element as ContentControl)?.Content is IScrollableScreen screen)
            {
                return new GuiScrollbar(screen.MaximumPosition, screen.PageSize, () => screen.FirstVisibleLine, screen.ScrollTo);
            }

            return base.NewScrollbar(isVertical);
        }
        #endregion
    }
}
