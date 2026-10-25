namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the W3C element rectangle: the top-left corner and size of a control in screen pixels.
    /// </summary>
    /// <remarks>
    /// This is the shape the W3C "Get Element Rect" command returns. It differs from the edge-based
    /// <see cref="RectangleModel"/> used by the UIA-compatible endpoints, which is kept for those endpoints only.
    /// </remarks>
    public class ElementRectModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the height in pixels.
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Gets or sets the width in pixels.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the horizontal screen coordinate of the left edge.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the vertical screen coordinate of the top edge.
        /// </summary>
        public int Y { get; set; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Converts an edge-based rectangle to the W3C origin-and-size shape.
        /// </summary>
        /// <param name="rectangle">The edge-based rectangle.</param>
        /// <returns>The W3C rectangle; an empty rectangle when the input is null.</returns>
        public static ElementRectModel ConvertFromRectangle(RectangleModel rectangle)
        {
            // Treat a missing rectangle as empty geometry rather than failing.
            if (rectangle is null)
            {
                return new ElementRectModel();
            }

            return new ElementRectModel
            {
                Height = rectangle.Bottom - rectangle.Top,
                Width = rectangle.Right - rectangle.Left,
                X = rectangle.Left,
                Y = rectangle.Top
            };
        }
        #endregion
    }
}
