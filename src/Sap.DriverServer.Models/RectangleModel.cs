namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents a screen rectangle in pixels, using the same edge-based shape as the UIA driver server.
    /// </summary>
    /// <remarks>
    /// SAP GUI Scripting exposes <c>ScreenLeft</c>, <c>ScreenTop</c>, <c>Width</c>, and <c>Height</c>;
    /// the driver converts them to edges so clients share one rectangle contract with the UIA server.
    /// </remarks>
    public class RectangleModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the bottom edge coordinate.
        /// </summary>
        public int Bottom { get; set; }

        /// <summary>
        /// Gets or sets the left edge coordinate.
        /// </summary>
        public int Left { get; set; }

        /// <summary>
        /// Gets or sets the right edge coordinate.
        /// </summary>
        public int Right { get; set; }

        /// <summary>
        /// Gets or sets the top edge coordinate.
        /// </summary>
        public int Top { get; set; }
        #endregion
    }
}
