using System.Runtime.InteropServices;
using System.Windows;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's picture control (<c>GuiShell</c> with <c>SubType</c> <c>Picture</c>).
    /// </summary>
    /// <remarks>
    /// A picture has no text or inner objects; scripts interact with it through clicks only. Each click is reported by
    /// the screen, which gives tests an observable result.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiPicture : GuiShell
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiPicture"/> class.
        /// </summary>
        /// <param name="element">The picture.</param>
        public GuiPicture(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public override string SubType => "Picture";

        // Gets the wrapped picture.
        private PictureHost Picture => (PictureHost)Element;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Clicks the picture.
        /// </summary>
        public void Click()
        {
            // Report a click without a position.
            Picture.Click();
        }

        /// <summary>
        /// Clicks a point of the picture.
        /// </summary>
        /// <param name="x">The horizontal picture coordinate.</param>
        /// <param name="y">The vertical picture coordinate.</param>
        public void ClickPictureArea(int x, int y)
        {
            // Report a click with its position in picture coordinates.
            Picture.ClickPictureArea(x, y);
        }

        /// <summary>
        /// Double-clicks the picture.
        /// </summary>
        public void DoubleClick()
        {
            // Report a double click.
            Picture.DoubleClick();
        }
        #endregion
    }
}
