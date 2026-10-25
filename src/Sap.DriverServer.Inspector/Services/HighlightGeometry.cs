using System;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Computes the physical-pixel bounds of a highlight overlay window for a SAP control rectangle.
    /// </summary>
    /// <remarks>
    /// The overlay draws a border just outside the control and a type/name tag above its top-left corner, in the
    /// style of the Accessibility Insights highlighter. Sizes are defined in device-independent pixels and scaled by
    /// the monitor DPI, because the overlay is positioned with <c>SetWindowPos</c> in physical pixels.
    /// </remarks>
    public static class HighlightGeometry
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the border thickness in device-independent pixels.
        /// </summary>
        public const double BorderDip = 2;

        /// <summary>
        /// Specifies the tag height in device-independent pixels.
        /// </summary>
        public const double TagHeightDip = 18;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Converts a control rectangle into overlay window bounds.
        /// </summary>
        /// <param name="element">The control rectangle in physical pixels.</param>
        /// <param name="scale">The monitor scale factor, such as 1.5 for 144 DPI.</param>
        /// <returns>The window bounds in physical pixels, or null when the rectangle is empty.</returns>
        public static HighlightBounds ConvertToWindowBounds(RectangleModel element, double scale)
        {
            // Skip objects without geometry, such as sessions or hidden controls.
            var width = element.Right - element.Left;
            var height = element.Bottom - element.Top;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            // Grow the rectangle by the border on every side and add the tag band above it.
            var normalizedScale = scale > 0 ? scale : 1;
            var border = (int)Math.Ceiling(BorderDip * normalizedScale);
            var tag = (int)Math.Ceiling(TagHeightDip * normalizedScale);

            return new HighlightBounds
            {
                Height = height + (2 * border) + tag,
                TagHeight = tag,
                Width = width + (2 * border),
                X = element.Left - border,
                Y = element.Top - border - tag
            };
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents overlay window bounds in physical pixels.
        /// </summary>
        public class HighlightBounds
        {
            /// <summary>
            /// Gets or sets the window height, including the tag band.
            /// </summary>
            public int Height { get; set; }

            /// <summary>
            /// Gets or sets the tag band height at the top of the window.
            /// </summary>
            public int TagHeight { get; set; }

            /// <summary>
            /// Gets or sets the window width.
            /// </summary>
            public int Width { get; set; }

            /// <summary>
            /// Gets or sets the left edge.
            /// </summary>
            public int X { get; set; }

            /// <summary>
            /// Gets or sets the top edge.
            /// </summary>
            public int Y { get; set; }
        }
        #endregion
    }
}
