using System;
using System.Globalization;
using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiScrollbar</c>: a scroll bar of a table control, a scroll container, or the user area.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The owner decides what a position means: the first visible row of a table control, the first visible line of a
    /// list, or a pixel offset of a scroll container. Setting the position scrolls the owner as a server round trip
    /// does, so identifiers that depend on the visible area, such as table cells and list labels, then name other data.
    /// </para>
    /// <para>
    /// A scroll bar of content that does not scroll has the maximum 0 and accepts only the position 0.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiScrollbar : ApartmentBoundObject
    {
        #region *** Fields       ***
        // Reads the owner's current position.
        private readonly Func<int> _getPosition;

        // Scrolls the owner to a validated position.
        private readonly Action<int> _setPosition;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiScrollbar"/> class.
        /// </summary>
        /// <param name="maximum">The highest position.</param>
        /// <param name="pageSize">The amount one page shows.</param>
        /// <param name="getPosition">Reads the owner's current position.</param>
        /// <param name="setPosition">Scrolls the owner to a position within 0 and <paramref name="maximum"/>.</param>
        internal GuiScrollbar(int maximum, int pageSize, Func<int> getPosition, Action<int> setPosition)
        {
            Maximum = Math.Max(0, maximum);
            PageSize = pageSize;
            _getPosition = getPosition;
            _setPosition = setPosition;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the highest position.
        /// </summary>
        public int Maximum { get; }

        /// <summary>
        /// Gets the lowest position, always 0.
        /// </summary>
        public int Minimum => 0;

        /// <summary>
        /// Gets the amount one page shows, such as the number of visible lines.
        /// </summary>
        public int PageSize { get; }

        /// <summary>
        /// Gets or sets the position; setting it scrolls the owner.
        /// </summary>
        public int Position
        {
            get => _getPosition();
            set => SetPosition(value);
        }

        /// <summary>
        /// Gets the size of the scroll range: the maximum minus the minimum.
        /// </summary>
        public int Range => Maximum - Minimum;

        /// <summary>
        /// Gets the scripting type name.
        /// </summary>
        public string Type => nameof(GuiScrollbar);
        #endregion

        #region *** Methods      ***
        // Scrolls the owner after checking the position against the range.
        private void SetPosition(int position)
        {
            // Reject positions outside the range with a message that names it.
            if (position < Minimum || position > Maximum)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"The scroll position {position} is outside {Minimum} to {Maximum}.");
                throw new SimulatorException(message);
            }

            _setPosition(position);
        }
        #endregion
    }
}
