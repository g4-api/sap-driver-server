using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;

using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Mvvm;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.ViewModels
{
    /// <summary>
    /// Holds the screen canvas of a driver session: the window screenshot and the rectangles drawn over it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The canvas works in image pixels. The view sizes its drawing surface to the image and scales the whole surface,
    /// so mouse positions arrive in image pixels too. Converting between image pixels and screen coordinates uses the
    /// captured window's screen rectangle; the image can be larger or smaller than that rectangle (window frame,
    /// display scaling), so both axes are scaled independently.
    /// </para>
    /// <para>
    /// Highlights are COM-free value objects. The selection is always the first highlight; locator matches follow.
    /// Every member runs on the UI thread.
    /// </para>
    /// <para>
    /// Tags and frames are drawn like the desktop overlay: the frame just outside the control and the tag outside the
    /// frame, above it, or below it when there is no room above, or inside as a last resort. The view shrinks the
    /// screenshot to fit, so it reports <see cref="DisplayScale"/> and the tag and frame line are counter-scaled to keep
    /// the desktop overlay's on-screen size at any zoom.
    /// </para>
    /// </remarks>
    public sealed class ScreenViewModel : ObservableObject
    {
        #region *** Fields       ***
        // Holds the last capture, used to convert between image pixels and screen coordinates.
        private ScreenCapture _capture;

        // Backs Caption.
        private string _caption = "No screenshot yet.";

        // Backs DisplayScale; one until the view reports how far it shrank the screenshot.
        private double _displayScale = 1;

        // Backs Image.
        private BitmapSource _image;

        // Holds the locator matches in screen coordinates, kept when the selection changes and redrawn per capture.
        private List<(RectangleModel Rectangle, string Label)> _matches = [];

        // Holds the selection in screen coordinates, or a null rectangle; redrawn per capture.
        private (RectangleModel Rectangle, string Label) _selection;
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the caption under the canvas: the captured window and the capture time.
        /// </summary>
        public string Caption
        {
            get => _caption;
            private set => SetProperty(ref _caption, value);
        }

        /// <summary>
        /// Gets or sets the on-screen size of one image pixel, in device-independent pixels; one at full size.
        /// </summary>
        /// <remarks>
        /// The view sets it whenever it rescales the screenshot. A change redraws the highlights, because tag sizes and
        /// placements depend on it. Values that are not positive are ignored.
        /// </remarks>
        public double DisplayScale
        {
            get => _displayScale;
            set
            {
                // Ignore invalid scales and tiny layout jitter; redraw only on a real change.
                if (value <= 0 || Math.Abs(value - _displayScale) < 0.001)
                {
                    return;
                }

                SetProperty(ref _displayScale, value);
                OnPropertyChanged(nameof(TagMaxWidth));
                UpdateHighlights();
            }
        }

        /// <summary>
        /// Gets the rectangles drawn over the screenshot, in image pixels: the selection first, then locator matches.
        /// </summary>
        public ObservableCollection<CanvasHighlight> Highlights { get; } = [];

        /// <summary>
        /// Gets the screenshot, or null before the first capture.
        /// </summary>
        public BitmapSource Image
        {
            get => _image;
            private set => SetProperty(ref _image, value);
        }

        /// <summary>
        /// Gets the screenshot height in pixels; zero before the first capture.
        /// </summary>
        public int ImageHeight => _image?.PixelHeight ?? 0;

        /// <summary>
        /// Gets the screenshot width in pixels; zero before the first capture.
        /// </summary>
        public int ImageWidth => _image?.PixelWidth ?? 0;

        /// <summary>
        /// Gets the widest a tag may be before its scale is applied: the screenshot's on-screen width.
        /// </summary>
        /// <remarks>
        /// Tags are counter-scaled, so their own width is in on-screen units; a longer label is trimmed with an ellipsis.
        /// </remarks>
        public double TagMaxWidth => ImageWidth * DisplayScale;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Shows a new capture and redraws the current highlights against it.
        /// </summary>
        /// <param name="capture">The capture; its PNG bytes are decoded and frozen.</param>
        public void ApplyCapture(ScreenCapture capture)
        {
            // Require a capture; the source always returns one or fails.
            ArgumentNullException.ThrowIfNull(argument: capture);

            // Decode the PNG once and freeze it, so the view can render it on any thread.
            using var stream = new MemoryStream(capture.Image);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            // Publish the image and its size, then redraw.
            _capture = capture;
            Image = bitmap;
            OnPropertyChanged(nameof(ImageWidth));
            OnPropertyChanged(nameof(ImageHeight));
            OnPropertyChanged(nameof(TagMaxWidth));
            Caption = $"{capture.WindowId}  ·  captured {DateTime.Now:HH:mm:ss}";
            UpdateHighlights();
        }

        /// <summary>
        /// Removes the screenshot and every highlight, for example after switching sources.
        /// </summary>
        public void Clear()
        {
            // Forget the capture and the stored rectangles, then publish the empty state.
            _capture = null;
            _selection = default;
            _matches = [];
            Image = null;
            OnPropertyChanged(nameof(ImageWidth));
            OnPropertyChanged(nameof(ImageHeight));
            OnPropertyChanged(nameof(TagMaxWidth));
            Caption = "No screenshot yet.";
            Highlights.Clear();
        }

        /// <summary>
        /// Converts a canvas position in image pixels to a screen point for hit testing.
        /// </summary>
        /// <param name="imageX">The horizontal position in image pixels.</param>
        /// <param name="imageY">The vertical position in image pixels.</param>
        /// <returns>The screen point with window handle 0 (any window of the session), or null before the first capture.</returns>
        public InspectorPoint ConvertToScreenPoint(double imageX, double imageY)
        {
            // Without a capture there is no mapping.
            if (_capture is null || ImageWidth == 0 || ImageHeight == 0)
            {
                return null;
            }

            // Scale each axis from image pixels to the window's screen rectangle.
            var (scaleX, scaleY) = GetScale();
            var x = _capture.WindowRect.X + (int)Math.Round(imageX * scaleX);
            var y = _capture.WindowRect.Y + (int)Math.Round(imageY * scaleY);

            return new InspectorPoint(x, y, WindowHandle: 0);
        }

        /// <summary>
        /// Removes the locator-match highlights and keeps the selection.
        /// </summary>
        public void HideMatches()
        {
            // Drop the matches and redraw the selection alone.
            _matches = [];
            UpdateHighlights();
        }

        /// <summary>
        /// Draws the locator matches.
        /// </summary>
        /// <param name="matches">The match rectangles in screen coordinates and their tag labels.</param>
        public void ShowMatches(IList<(RectangleModel Rectangle, string Label)> matches)
        {
            // Keep the screen rectangles; drawing converts them against the current capture.
            _matches = [.. matches ?? []];
            UpdateHighlights();
        }

        /// <summary>
        /// Draws the selection, or removes it when the rectangle is null.
        /// </summary>
        /// <param name="rectangle">The selection rectangle in screen coordinates, or null.</param>
        /// <param name="label">The tag text.</param>
        public void ShowSelection(RectangleModel rectangle, string label)
        {
            // Keep the screen rectangle; drawing converts it against the current capture.
            _selection = (rectangle, label);
            UpdateHighlights();
        }

        // Converts a screen rectangle to image pixels; null when it is empty or entirely outside the captured window.
        private CanvasHighlight ConvertToHighlight(RectangleModel rectangle, string label, bool isMatch)
        {
            // Without a capture or geometry there is nothing to draw.
            if (_capture is null || rectangle is null || rectangle.Right <= rectangle.Left || rectangle.Bottom <= rectangle.Top)
            {
                return null;
            }

            // Map both corners into image pixels and clip to the image.
            var (scaleX, scaleY) = GetScale();
            var left = Math.Max(0, (rectangle.Left - _capture.WindowRect.X) / scaleX);
            var top = Math.Max(0, (rectangle.Top - _capture.WindowRect.Y) / scaleY);
            var right = Math.Min(ImageWidth, (rectangle.Right - _capture.WindowRect.X) / scaleX);
            var bottom = Math.Min(ImageHeight, (rectangle.Bottom - _capture.WindowRect.Y) / scaleY);

            if (right <= left || bottom <= top)
            {
                return null;
            }

            // Counter-scale the tag and the frame line, so they keep the desktop overlay's size on screen.
            var tagScale = 1 / DisplayScale;
            var frame = HighlightGeometry.BorderDip * tagScale;
            var tagHeight = HighlightGeometry.TagHeightDip * tagScale;

            // Place the tag outside the frame: above, else below, else inside the top-left corner.
            var (placement, tagY) = (top - frame - tagHeight >= 0, bottom + frame + tagHeight <= ImageHeight) switch
            {
                (true, _) => (CanvasTagPlacement.Above, top - frame - tagHeight),
                (false, true) => (CanvasTagPlacement.Below, bottom + frame),
                _ => (CanvasTagPlacement.Inside, top)
            };

            return new CanvasHighlight
            {
                FrameThickness = frame,
                Height = bottom - top,
                IsMatch = isMatch,
                Label = label,
                TagPlacement = placement,
                TagScale = tagScale,
                TagY = tagY,
                Width = right - left,
                X = left,
                Y = top
            };
        }

        // Gets the screen pixels per image pixel on each axis; 1 when the window size is unknown.
        private (double ScaleX, double ScaleY) GetScale()
        {
            // Divide the window size by the image size per axis; an unknown size keeps a one-to-one mapping.
            var rect = _capture.WindowRect;
            var scaleX = rect.Width > 0 && ImageWidth > 0 ? (double)rect.Width / ImageWidth : 1.0;
            var scaleY = rect.Height > 0 && ImageHeight > 0 ? (double)rect.Height / ImageHeight : 1.0;

            return (scaleX, scaleY);
        }

        // Rebuilds the highlight collection against the current capture: the selection first, then the matches.
        // Rectangles that are empty or outside the captured window are skipped.
        private void UpdateHighlights()
        {
            // Convert the selection and the matches against the current capture, then republish the visible ones.
            var highlights = new List<CanvasHighlight> { ConvertToHighlight(_selection.Rectangle, _selection.Label, isMatch: false) };
            highlights.AddRange(_matches.Select(i => ConvertToHighlight(i.Rectangle, i.Label, isMatch: true)));

            Highlights.Clear();
            foreach (var highlight in highlights.Where(i => i is not null))
            {
                Highlights.Add(highlight);
            }
        }
        #endregion
    }

    /// <summary>
    /// Represents one rectangle drawn over the screenshot, in image pixels.
    /// </summary>
    /// <remarks>
    /// The record is immutable once built and compares by value, so tests and the view can compare highlights directly.
    /// </remarks>
    public sealed record CanvasHighlight
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the frame line thickness in image pixels; the frame is drawn just outside the rectangle.
        /// </summary>
        public double FrameThickness { get; init; }

        /// <summary>
        /// Gets the frame's outward offset: the negative frame thickness on every side, for the view's margin.
        /// </summary>
        public Thickness FrameOffset => new(-FrameThickness);

        /// <summary>
        /// Gets the height in image pixels.
        /// </summary>
        public double Height { get; init; }

        /// <summary>
        /// Gets a value indicating whether the rectangle is a locator match; false for the selection.
        /// </summary>
        public bool IsMatch { get; init; }

        /// <summary>
        /// Gets the tag text, such as <c>GuiButton btn[3]</c>.
        /// </summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>
        /// Gets where the tag sits: one of the <see cref="CanvasTagPlacement"/> values.
        /// </summary>
        public string TagPlacement { get; init; } = CanvasTagPlacement.Above;

        /// <summary>
        /// Gets the factor that sizes the tag in image pixels, so it shows at the desktop overlay's size on screen.
        /// </summary>
        public double TagScale { get; init; } = 1;

        /// <summary>
        /// Gets the top edge of the tag in image pixels.
        /// </summary>
        public double TagY { get; init; }

        /// <summary>
        /// Gets the width in image pixels.
        /// </summary>
        public double Width { get; init; }

        /// <summary>
        /// Gets the left edge in image pixels.
        /// </summary>
        public double X { get; init; }

        /// <summary>
        /// Gets the top edge in image pixels.
        /// </summary>
        public double Y { get; init; }
        #endregion
    }

    /// <summary>
    /// Names where a canvas highlight's tag sits relative to its frame.
    /// </summary>
    /// <remarks>
    /// The tag goes above the frame when the screenshot has room, below it otherwise, and inside the frame's top-left
    /// corner only when neither fits, such as for a control that fills the whole window.
    /// </remarks>
    public static class CanvasTagPlacement
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies a tag above the frame, the default, as on the desktop overlay.
        /// </summary>
        public const string Above = "Above";

        /// <summary>
        /// Specifies a tag below the frame, for controls at the top edge of the screenshot.
        /// </summary>
        public const string Below = "Below";

        /// <summary>
        /// Specifies a tag inside the frame's top-left corner, when there is no room above or below.
        /// </summary>
        public const string Inside = "Inside";
        #endregion
    }
}
