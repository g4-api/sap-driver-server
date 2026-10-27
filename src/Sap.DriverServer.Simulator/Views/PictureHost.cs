using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows a picture control (<c>GuiPicture</c>): a drawn image that reports clicks.
    /// </summary>
    /// <remarks>
    /// Clicks, by mouse or script, are handed to <see cref="ClickHandler"/> as a description: <c>Click</c>,
    /// <c>DoubleClick</c>, or <c>PictureArea(x,y)</c> with the point in picture coordinates. The image is drawn from
    /// shapes, so the simulator ships no image files.
    /// </remarks>
    public sealed class PictureHost : Border
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="PictureHost"/> class.
        /// </summary>
        public PictureHost()
        {
            // Draw a simple chart-like picture from shapes on a light background.
            var canvas = new Canvas { Background = Brushes.WhiteSmoke, ClipToBounds = true };
            for (var index = 0; index < 5; index++)
            {
                var bar = new Rectangle { Fill = Brushes.SteelBlue, Height = 20 + (index * 18), Width = 22 };
                Canvas.SetLeft(bar, 16 + (index * 32));
                Canvas.SetBottom(bar, 12);
                canvas.Children.Add(bar);
            }

            Child = canvas;
            MouseLeftButtonUp += (_, e) => RaiseClick(e.GetPosition(this));
            MouseLeftButtonDown += (_, e) => RaiseDoubleClickWhenRepeated(e);
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the handler that receives the click descriptions.
        /// </summary>
        public Action<string> ClickHandler { get; set; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Reports a click on the picture.
        /// </summary>
        public void Click()
        {
            ClickHandler?.Invoke("Click");
        }

        /// <summary>
        /// Reports a click at a point of the picture.
        /// </summary>
        /// <param name="x">The horizontal picture coordinate.</param>
        /// <param name="y">The vertical picture coordinate.</param>
        public void ClickPictureArea(int x, int y)
        {
            ClickHandler?.Invoke(string.Create(CultureInfo.InvariantCulture, $"PictureArea({x},{y})"));
        }

        /// <summary>
        /// Reports a double click on the picture.
        /// </summary>
        public void DoubleClick()
        {
            ClickHandler?.Invoke("DoubleClick");
        }

        // Reports a mouse click as a picture area click at the mouse position.
        private void RaiseClick(Point point)
        {
            ClickPictureArea((int)point.X, (int)point.Y);
        }

        // Reports a mouse double click; the single click of the same gesture is reported too, as SAP raises both.
        private void RaiseDoubleClickWhenRepeated(MouseButtonEventArgs e)
        {
            // Only the second press of a double click counts.
            if (e.ClickCount == 2)
            {
                DoubleClick();
            }
        }
        #endregion
    }
}
