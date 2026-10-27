using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Inspector.ViewModels;
using Sap.DriverServer.Inspector.Views;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("Inspector")]
    [TestCategory("UnitTest")]
    public class ScreenViewModelTests
    {
        [TestMethod(DisplayName = "Verify that canvas positions map to screen points through the captured window's scale.")]
        public void ConvertToScreenPointTest()
        {
            // Arrange: a 200x100 image of a 400x200 window at (1000, 500), so each image pixel covers two screen pixels.
            var screen = new ScreenViewModel();
            var before = screen.ConvertToScreenPoint(10, 20);
            screen.ApplyCapture(NewCapture(width: 200, height: 100));

            // Act: convert a canvas position.
            var point = screen.ConvertToScreenPoint(10, 20);

            // Assert: no mapping exists before the capture; afterwards both axes are scaled and offset.
            Assert.IsNull(before);
            Assert.AreEqual(new InspectorPoint(1020, 540, WindowHandle: 0), point);
            Assert.AreEqual(200, screen.ImageWidth);
        }

        [TestMethod(DisplayName = "Verify that highlights are drawn in image pixels, redrawn per capture, and clipped to the window.")]
        public void HighlightsTest()
        {
            // Arrange: a selection set before any capture, and a match outside the window.
            var screen = new ScreenViewModel();
            screen.ShowSelection(new RectangleModel { Left = 1100, Top = 600, Right = 1200, Bottom = 700 }, "GuiButton btnOK");
            var beforeCapture = screen.Highlights.Count;
            screen.ShowMatches([(new RectangleModel { Left = 1, Top = 1, Right = 5, Bottom = 5 }, "#1")]);

            // Act: apply the capture, then hide the matches and clear.
            screen.ApplyCapture(NewCapture(width: 200, height: 100));
            var selection = screen.Highlights.Single();
            screen.HideMatches();
            var afterHide = screen.Highlights.Count;
            screen.Clear();

            // Assert: the selection appears once a capture exists, at half scale; the outside match is skipped.
            Assert.AreEqual(0, beforeCapture);
            var expected = new CanvasHighlight
            {
                FrameThickness = 2,
                Height = 50,
                IsMatch = false,
                Label = "GuiButton btnOK",
                TagPlacement = CanvasTagPlacement.Above,
                TagScale = 1,
                TagY = 30,
                Width = 50,
                X = 50,
                Y = 50
            };
            Assert.AreEqual(expected, selection);
            Assert.AreEqual(1, afterHide);
            Assert.IsEmpty(screen.Highlights);
            Assert.IsNull(screen.Image);
        }

        #region *** Data Set ***
        [DataRow(1100, 600, 1200, 700, CanvasTagPlacement.Above, 30, DisplayName = "Verify that a tag sits above a control with room above it.")]
        [DataRow(1100, 500, 1200, 540, CanvasTagPlacement.Below, 22, DisplayName = "Verify that a tag moves below a control at the top edge.")]
        [DataRow(1000, 500, 1400, 700, CanvasTagPlacement.Inside, 0, DisplayName = "Verify that a tag stays inside a control that fills the window.")]
        #endregion
        [TestMethod]
        public void TagPlacementTest(int left, int top, int right, int bottom, string expectedPlacement, double expectedTagY)
        {
            // Arrange: a 200x100 image of a 400x200 window, shown at full size.
            var screen = new ScreenViewModel();
            screen.ApplyCapture(NewCapture(width: 200, height: 100));

            // Act: select the control.
            screen.ShowSelection(new RectangleModel { Left = left, Top = top, Right = right, Bottom = bottom }, "GuiCtrl");
            var highlight = screen.Highlights.Single();

            // Assert: the tag is placed outside the frame when it fits, and inside only as a last resort.
            Assert.AreEqual(expectedPlacement, highlight.TagPlacement);
            Assert.AreEqual(expectedTagY, highlight.TagY, 0.001);
        }

        [TestMethod(DisplayName = "Verify that a shrunk screenshot counter-scales the tag and the frame line.")]
        public void DisplayScaleTest()
        {
            // Arrange: a selection on a 200x100 image.
            var screen = new ScreenViewModel();
            screen.ApplyCapture(NewCapture(width: 200, height: 100));
            screen.ShowSelection(new RectangleModel { Left = 1100, Top = 600, Right = 1200, Bottom = 700 }, "GuiCtrl");

            // Act: the view shows the screenshot at half size; an invalid scale is then ignored.
            screen.DisplayScale = 0.5;
            screen.DisplayScale = 0;
            var highlight = screen.Highlights.Single();

            // Assert: the tag and frame double in image pixels, the tag still fits above, and its width limit follows.
            Assert.AreEqual(0.5, screen.DisplayScale);
            Assert.AreEqual(2, highlight.TagScale);
            Assert.AreEqual(4, highlight.FrameThickness);
            Assert.AreEqual(CanvasTagPlacement.Above, highlight.TagPlacement);
            Assert.AreEqual(10, highlight.TagY, 0.001);
            Assert.AreEqual(100, screen.TagMaxWidth);
        }

        #region *** Data Set ***
        [DataRow(50d, 60d, 48d, DisplayName = "Verify that a tag starts at the frame's outer left edge.")]
        [DataRow(180d, 60d, 140d, DisplayName = "Verify that a tag near the right edge shifts left to stay inside.")]
        [DataRow(1d, 60d, 0d, DisplayName = "Verify that a tag never starts before the screenshot.")]
        [DataRow(10d, 300d, 0d, DisplayName = "Verify that a tag wider than the screenshot starts at 0.")]
        #endregion
        [TestMethod]
        public void CanvasTagLeftTest(double left, double tagWidth, double expected)
        {
            // Arrange: a 200-pixel screenshot, a 2-pixel frame, and a tag at scale 1.
            var converter = new CanvasTagLeftConverter();

            // Act: compute the tag's left edge.
            var result = converter.Convert([left, 2d, 200, tagWidth, 1d], typeof(double), parameter: null, CultureInfo.InvariantCulture);

            // Assert: the tag starts at the frame, or as far left as it needs to end inside the screenshot.
            Assert.AreEqual(expected, (double)result, 0.001);
        }

        [TestMethod(DisplayName = "Verify that driver sessions offer Enter, all function key groups, and the paging keys.")]
        public void StandardVirtualKeysTest()
        {
            // Act: read the standard keys.
            var keys = VirtualKeyCatalog.GetStandardKeys();

            // Assert: 1 + 48 + 4 keys with the documented names.
            Assert.HasCount(53, keys);
            Assert.AreEqual("Enter", keys.Single(i => i.Key == 0).Description);
            Assert.AreEqual("Shift+F1", keys.Single(i => i.Key == 13).Description);
            Assert.AreEqual("Ctrl+Shift+F12", keys.Single(i => i.Key == 48).Description);
            Assert.AreEqual("Page Down", keys.Single(i => i.Key == 82).Description);
        }

        // Builds a capture of a 400x200 window at (1000, 500) whose PNG has the given pixel size.
        private static ScreenCapture NewCapture(int width, int height)
        {
            // Encode a blank bitmap as PNG, the format the driver returns.
            var pixels = new byte[width * height * 4];
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, palette: null, pixels, width * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);

            return new ScreenCapture
            {
                Image = stream.ToArray(),
                WindowId = "/app/con[0]/ses[0]/wnd[0]",
                WindowRect = new ElementRectModel { Height = 200, Width = 400, X = 1000, Y = 500 }
            };
        }
    }
}
