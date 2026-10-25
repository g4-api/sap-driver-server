using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Inspector.ViewModels;
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
            var expected = new CanvasHighlight { Height = 50, IsMatch = false, Label = "GuiButton btnOK", Width = 50, X = 50, Y = 50 };
            Assert.AreEqual(expected, selection);
            Assert.AreEqual(1, afterHide);
            Assert.IsEmpty(screen.Highlights);
            Assert.IsNull(screen.Image);
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
