namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Defines a screen whose content scrolls by line, such as a list, through the user area's vertical scroll bar.
    /// </summary>
    /// <remarks>
    /// Scrolling such a screen replaces what the visible lines show, as a server round trip in SAP does, so element
    /// identifiers that carry a line number name other data afterwards.
    /// </remarks>
    public interface IScrollableScreen
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the zero-based first visible line.
        /// </summary>
        int FirstVisibleLine { get; }

        /// <summary>
        /// Gets the highest first visible line.
        /// </summary>
        int MaximumPosition { get; }

        /// <summary>
        /// Gets the number of lines one page shows.
        /// </summary>
        int PageSize { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Scrolls so a line is the first visible line.
        /// </summary>
        /// <param name="line">The new first visible line, from 0 to <see cref="MaximumPosition"/>.</param>
        void ScrollTo(int line);
        #endregion
    }
}
