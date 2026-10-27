using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiScrollContainer</c>: a subscreen container with scroll bars (<c>ssub</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every field of a scroll container is part of the scripting tree, also when it is scrolled out of view; such a
    /// field reports coordinates outside the container. Setting <c>verticalScrollbar.position</c> brings it into view.
    /// Positions of a scroll container are pixel offsets in the simulator.
    /// </para>
    /// <para>
    /// A container that does not scroll reports scroll bars with the maximum 0. The <c>ssub</c> prefix follows the SAP
    /// GUI Scripting documentation, where <c>sub</c> names a <c>GuiSimpleContainer</c> instead.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public class GuiScrollContainer : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiScrollContainer"/> class.
        /// </summary>
        /// <param name="element">The WPF element; a scroll viewer when the container scrolls.</param>
        public GuiScrollContainer(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the horizontal scroll bar.
        /// </summary>
        public GuiScrollbar HorizontalScrollbar => NewScrollbar(isVertical: false);

        /// <summary>
        /// Gets the vertical scroll bar.
        /// </summary>
        public GuiScrollbar VerticalScrollbar => NewScrollbar(isVertical: true);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Creates a scroll bar for the container.
        /// </summary>
        /// <param name="isVertical">True for the vertical scroll bar; false for the horizontal one.</param>
        /// <returns>The scroll bar; one with the maximum 0 when the container does not scroll that way.</returns>
        protected virtual GuiScrollbar NewScrollbar(bool isVertical)
        {
            // Containers that are not scroll viewers cannot scroll.
            if (Element is not ScrollViewer viewer)
            {
                return new GuiScrollbar(maximum: 0, pageSize: 0, getPosition: () => 0, setPosition: _ => { });
            }

            // Map the scroll viewer's offsets to whole pixels; scrolling lays out at once, so geometry reads are current.
            viewer.UpdateLayout();
            if (isVertical)
            {
                return new GuiScrollbar(
                    maximum: (int)viewer.ScrollableHeight,
                    pageSize: (int)viewer.ViewportHeight,
                    getPosition: () => (int)viewer.VerticalOffset,
                    setPosition: i => ScrollTo(viewer, i, isVertical));
            }

            return new GuiScrollbar(
                maximum: (int)viewer.ScrollableWidth,
                pageSize: (int)viewer.ViewportWidth,
                getPosition: () => (int)viewer.HorizontalOffset,
                setPosition: i => ScrollTo(viewer, i, isVertical));
        }

        // Scrolls a scroll viewer to a pixel offset and lays it out, so the next geometry read sees the new position.
        private static void ScrollTo(ScrollViewer viewer, int offset, bool isVertical)
        {
            // Scroll along the requested axis, then lay out.
            if (isVertical)
            {
                viewer.ScrollToVerticalOffset(offset);
            }
            else
            {
                viewer.ScrollToHorizontalOffset(offset);
            }

            viewer.UpdateLayout();
        }
        #endregion
    }
}
