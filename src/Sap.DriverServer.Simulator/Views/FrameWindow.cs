using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Sap.DriverServer.Simulator.Simulation;

using Rectangle = System.Windows.Shapes.Rectangle;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Provides what every simulated SAP frame window (<c>wnd[n]</c>) shares: identity, highlights, and screen captures.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A frame window's scripting geometry is its client content (<see cref="ContentRoot"/>), not the Windows frame
    /// around it. <see cref="SaveImage"/> captures exactly that area, so a screenshot maps pixel for pixel onto the
    /// reported <c>ScreenLeft</c>, <c>ScreenTop</c>, <c>Width</c>, and <c>Height</c>.
    /// </para>
    /// <para>
    /// Highlights imitate SAP GUI's <c>Visualize</c> red frame. They are drawn on a click-through layer above the
    /// content and are removed when the screen changes, because their positions are not tracked through layout changes.
    /// </para>
    /// </remarks>
    public abstract class FrameWindow : Window
    {
        #region *** Constants    ***
        // Selects BMP in HardCopy's image type argument, which SAP GUI uses by default.
        private const int BitmapImageType = 0;

        // Selects GIF in HardCopy's image type argument.
        private const int GifImageType = 3;

        // Selects JPEG in HardCopy's image type argument.
        private const int JpegImageType = 1;

        // Selects PNG in HardCopy's image type argument.
        private const int PngImageType = 2;
        #endregion

        #region *** Fields       ***
        // Maps each highlighted element to the frame drawn around it.
        private readonly Dictionary<FrameworkElement, Rectangle> _highlights = [];
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="FrameWindow"/> class.
        /// </summary>
        /// <param name="session">The session that owns the window.</param>
        protected FrameWindow(SimulatedSession session)
        {
            // Require the owner; the window's identifier and every action go through it.
            ArgumentNullException.ThrowIfNull(argument: session);

            Session = session;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the client content that scripting measures, captures, and searches for controls.
        /// </summary>
        public abstract FrameworkElement ContentRoot { get; }

        /// <summary>
        /// Gets the Win32 window handle; zero before the window is shown.
        /// </summary>
        public long Handle => new WindowInteropHelper(this).Handle.ToInt64();

        /// <summary>
        /// Gets the identifier, such as <c>/app/con[0]/ses[0]/wnd[1]</c>.
        /// </summary>
        public string Id => string.Create(CultureInfo.InvariantCulture, $"{Session.Id}/wnd[{WindowIndex}]");

        /// <summary>
        /// Gets the window title SAP GUI reports, without the simulator suffix of the Windows caption.
        /// </summary>
        public abstract string ScreenTitle { get; }

        /// <summary>
        /// Gets the session that owns the window.
        /// </summary>
        public SimulatedSession Session { get; }

        /// <summary>
        /// Gets the zero-based window index: 0 for the main window, 1 for the popup.
        /// </summary>
        public abstract int WindowIndex { get; }

        /// <summary>
        /// Gets the click-through layer that holds the highlight frames.
        /// </summary>
        protected abstract Canvas HighlightLayer { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Removes every highlight frame.
        /// </summary>
        public void ClearHighlights()
        {
            // Remove every frame and forget the elements they belonged to.
            HighlightLayer.Children.Clear();
            _highlights.Clear();
        }

        /// <summary>
        /// Writes an image of the client content to a file, as SAP GUI's <c>HardCopy</c> does.
        /// </summary>
        /// <param name="fileName">The target file; a relative name is placed in the user's temporary folder.</param>
        /// <param name="imageType">The format: 0 BMP, 1 JPEG, 2 PNG, or 3 GIF.</param>
        /// <returns>The full path of the written file.</returns>
        /// <exception cref="ArgumentException">The image type is not one of the four formats.</exception>
        public string SaveImage(string fileName, int imageType)
        {
            // Pick the encoder first, so an unknown type fails before anything is rendered.
            BitmapEncoder encoder = imageType switch
            {
                BitmapImageType => new BmpBitmapEncoder(),
                JpegImageType => new JpegBitmapEncoder(),
                PngImageType => new PngBitmapEncoder(),
                GifImageType => new GifBitmapEncoder(),
                _ => throw new ArgumentException($"Image type {imageType} is not 0 (BMP), 1 (JPEG), 2 (PNG), or 3 (GIF).", nameof(imageType))
            };

            // Render the content at the monitor's scale, sized like the reported scripting geometry.
            var bounds = ScriptingTree.GetScreenBounds(ContentRoot);
            var dpi = VisualTreeHelper.GetDpi(ContentRoot);
            var pixelWidth = Math.Max(1, (int)Math.Round(bounds.Right) - (int)Math.Round(bounds.Left));
            var pixelHeight = Math.Max(1, (int)Math.Round(bounds.Bottom) - (int)Math.Round(bounds.Top));
            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();

            using (var context = visual.RenderOpen())
            {
                var area = new Rect(0, 0, ContentRoot.ActualWidth, ContentRoot.ActualHeight);
                context.DrawRectangle(new VisualBrush(ContentRoot), pen: null, area);
            }

            bitmap.Render(visual);

            // Drop the alpha channel, which BMP, JPEG, and GIF cannot hold, and write the file.
            var path = Path.GetFullPath(fileName, Path.GetTempPath());
            encoder.Frames.Add(BitmapFrame.Create(new FormatConvertedBitmap(bitmap, PixelFormats.Bgr24, destinationPalette: null, alphaThreshold: 0)));

            using (var stream = File.Create(path))
            {
                encoder.Save(stream);
            }

            return path;
        }

        /// <summary>
        /// Draws or removes a red frame around an element of this window.
        /// </summary>
        /// <param name="target">The element, such as a button or a grid cell.</param>
        /// <param name="isOn">True to draw the frame; false to remove it.</param>
        /// <returns>True when the frame state changed as requested; false when the element is not in this window.</returns>
        public bool SetHighlighted(FrameworkElement target, bool isOn)
        {
            // Remove an existing frame in both cases; turning on again redraws it at the current position.
            if (_highlights.Remove(target, out var existing))
            {
                HighlightLayer.Children.Remove(existing);
            }

            if (!isOn)
            {
                return true;
            }

            // Place the frame over the element; elements outside this window cannot be framed.
            Rect area;
            try
            {
                ContentRoot.UpdateLayout();
                area = target.TransformToAncestor(ContentRoot).TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            var frame = new Rectangle
            {
                Height = area.Height,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Width = area.Width
            };
            Canvas.SetLeft(frame, area.Left);
            Canvas.SetTop(frame, area.Top);
            HighlightLayer.Children.Add(frame);
            _highlights[target] = frame;

            return true;
        }
        #endregion
    }
}
