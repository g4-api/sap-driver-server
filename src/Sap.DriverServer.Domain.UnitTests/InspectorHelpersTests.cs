using System;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("InspectorHelpers")]
    [TestCategory("UnitTest")]
    public class InspectorHelpersTests
    {
        [TestMethod(DisplayName = "Verify that invoker arguments are typed as strings, Booleans, integers, and doubles.")]
        public void ConvertToArgumentsTypesTest()
        {
            // Act: parse a mixed argument list, including commas and spaces inside quotes.
            var arguments = ArgumentParser.ConvertToArguments("\"&MB_EXPORT\", 'a, b', 42, -7, 1.5, true, FALSE, plain text");

            // Assert: each token has the expected type and value.
            Assert.AreSequenceEqual(new object[] { "&MB_EXPORT", "a, b", 42, -7, 1.5, true, false, "plain text" }, arguments);
        }

        [TestMethod(DisplayName = "Verify that empty input yields no arguments and malformed quotes are rejected.")]
        #region *** Data Set ***
        [DataRow("\"unterminated")]
        [DataRow("\"a\" b")]
        [DataRow("x\"y\"")]
        #endregion
        public void ConvertToArgumentsInvalidTest(string text)
        {
            // Act and Assert: malformed quoting is a format error.
            Assert.ThrowsExactly<FormatException>(() => ArgumentParser.ConvertToArguments(text));
            Assert.IsEmpty(ArgumentParser.ConvertToArguments("   "));
        }

        [TestMethod(DisplayName = "Verify that overlay bounds frame the control with a DPI-scaled border and a tag band above it.")]
        #region *** Data Set ***
        [DataRow(1.0, 98, 80, 104, 42)]
        [DataRow(1.5, 97, 70, 106, 53)]
        #endregion
        public void ConvertToWindowBoundsTest(double scale, int x, int y, int width, int height)
        {
            // Arrange: a 100x20 control at (100, 100).
            var rectangle = new RectangleModel { Left = 100, Top = 100, Right = 200, Bottom = 120 };

            // Act: compute the overlay bounds.
            var bounds = HighlightGeometry.ConvertToWindowBounds(rectangle, scale);

            // Assert: border on every side, tag band above, all in physical pixels.
            Assert.AreEqual(x, bounds.X);
            Assert.AreEqual(y, bounds.Y);
            Assert.AreEqual(width, bounds.Width);
            Assert.AreEqual(height, bounds.Height);
            Assert.IsNull(HighlightGeometry.ConvertToWindowBounds(new RectangleModel(), scale));
        }

        [TestMethod(DisplayName = "Verify that quick actions follow the driver's control families.")]
        #region *** Data Set ***
        [DataRow("GuiButton", "", "Press,Set focus")]
        [DataRow("GuiTab", "", "Select,Set focus")]
        [DataRow("GuiCheckBox", "", "Toggle,Set focus")]
        [DataRow("GuiTextField", "", "Set focus,Set text,Clear")]
        [DataRow("GuiComboBox", "", "Set focus,Select key")]
        [DataRow("GuiShell", "TextEdit", "Set focus,Set text,Clear")]
        [DataRow("GuiShell", "GridView", "Set focus")]
        [DataRow("GuiSession", "", "")]
        #endregion
        public void GetActionsTest(string type, string subType, string expected)
        {
            // Act: list the actions for the control family.
            var labels = string.Join(",", ActionCatalog.GetActions(type, subType).Select(i => i.Label));

            // Assert: the labels match the family.
            Assert.AreEqual(expected, labels);
        }
    }
}
