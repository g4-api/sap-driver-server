using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("SapControlInteractions")]
    [TestCategory("UnitTest")]
    public class SapControlInteractionsTests
    {
        [TestMethod(DisplayName = "Verify that click invokes the documented scripting member for each control family.")]
        #region *** Data Set ***
        [DataRow("GuiButton", "press()")]
        [DataRow("GuiTab", "select()")]
        [DataRow("GuiRadioButton", "select()")]
        [DataRow("GuiMenu", "select()")]
        [DataRow("GuiTextField", "setFocus()")]
        [DataRow("GuiCTextField", "setFocus()")]
        [DataRow("GuiLabel", "setFocus()")]
        [DataRow("GuiComboBox", "setFocus()")]
        #endregion
        public void InvokeClickMappingTest(string type, string expectedInvocation)
        {
            // Arrange: a control of the given family.
            var node = NewControl(type);

            // Act: click it.
            SapControlInteractions.InvokeClick(node);

            // Assert: exactly the documented member was invoked.
            var expectedInvocations = new[] { expectedInvocation };
            Assert.AreSequenceEqual(expectedInvocations, node.Invocations);
        }

        [TestMethod(DisplayName = "Verify that clicking a check box toggles its selected property.")]
        public void InvokeClickTogglesCheckBoxTest()
        {
            // Arrange: an unchecked check box.
            var node = NewControl("GuiCheckBox");
            node.Properties["Selected"] = false;

            // Act: click twice.
            SapControlInteractions.InvokeClick(node);
            var afterFirst = node.Properties["Selected"];
            SapControlInteractions.InvokeClick(node);

            // Assert: the state toggled on and then off.
            Assert.IsTrue((bool)afterFirst);
            Assert.IsFalse((bool)node.Properties["Selected"]);
        }

        [TestMethod(DisplayName = "Verify that clicking a shell or unknown control is an unsupported operation and nothing is invoked.")]
        #region *** Data Set ***
        [DataRow("GuiShell")]
        [DataRow("GuiUserArea")]
        #endregion
        public void InvokeClickUnsupportedTest(string type)
        {
            // Arrange: a control family without a click equivalent.
            var node = NewControl(type);
            node.Properties["SubType"] = "GridView";

            // Act: click it.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SapControlInteractions.InvokeClick(node));

            // Assert: the failure is explicit and no member was invoked.
            Assert.AreEqual(WebDriverErrorCodes.UnsupportedOperation, exception.Error);
            Assert.AreEqual(SapErrorKinds.NotExposedByScripting, exception.ErrorKind);
            Assert.IsEmpty(node.Invocations);
        }

        [TestMethod(DisplayName = "Verify that SetText writes text fields and text edits, and selects combo box entries by key.")]
        #region *** Data Set ***
        [DataRow("GuiTextField", null, "set Text=abc")]
        [DataRow("GuiPasswordField", null, "set Text=abc")]
        [DataRow("GuiOkCodeField", null, "set Text=abc")]
        [DataRow("GuiShell", "TextEdit", "set Text=abc")]
        [DataRow("GuiComboBox", null, "set Key=abc")]
        #endregion
        public void SetTextMappingTest(string type, string subType, string expectedInvocation)
        {
            // Arrange: a writable control.
            var node = NewControl(type);
            if (subType is not null)
            {
                node.Properties["SubType"] = subType;
            }

            // Act: write the text.
            SapControlInteractions.SetText(node, "abc");

            // Assert: the matching property was assigned.
            var expectedInvocations = new[] { expectedInvocation };
            Assert.AreSequenceEqual(expectedInvocations, node.Invocations);
        }

        [TestMethod(DisplayName = "Verify that writing or clearing a read-only control is element not interactable (HTTP 400).")]
        public void SetTextNotInteractableTest()
        {
            // Arrange: a label and a combo box.
            var label = NewControl("GuiLabel");
            var comboBox = NewControl("GuiComboBox");

            // Act: write the label and clear the combo box.
            var writeException = Assert.ThrowsExactly<SapDriverException>(() => SapControlInteractions.SetText(label, "x"));
            var clearException = Assert.ThrowsExactly<SapDriverException>(() => SapControlInteractions.ClearText(comboBox));

            // Assert: both are 400 "element not interactable" failures.
            Assert.AreEqual(WebDriverErrorCodes.ElementNotInteractable, writeException.Error);
            Assert.AreEqual(400, writeException.StatusCode);
            Assert.AreEqual(WebDriverErrorCodes.ElementNotInteractable, clearException.Error);
        }

        [TestMethod(DisplayName = "Verify that password text is never returned through text or attribute reads.")]
        public void GetTextPasswordTest()
        {
            // Arrange: a filled password field.
            var node = NewControl("GuiPasswordField");
            node.Properties["Text"] = "TopSecret1";
            node.Properties["DisplayedText"] = "TopSecret1";

            // Act: read the text, the text attribute, and the displayed text attribute.
            var text = SapControlInteractions.GetText(node);
            var attribute = SapControlInteractions.GetAttribute(node, "TEXT");
            var displayed = SapControlInteractions.GetAttribute(node, "displayedText");

            // Assert: all are empty, and neither text property was ever read from SAP.
            Assert.AreEqual(string.Empty, text);
            Assert.AreEqual(string.Empty, attribute);
            Assert.AreEqual(string.Empty, displayed);
            Assert.DoesNotContain("DisplayedText", node.PropertyReads);
        }

        [TestMethod(DisplayName = "Verify that dotted attribute names read a scroll bar's values and read as empty without a scroll bar.")]
        #region *** Data Set ***
        [DataRow("GuiTableControl", "verticalScrollbar.position", "12")]
        [DataRow("GuiTableControl", "VerticalScrollbar.Maximum", "199")]
        [DataRow("GuiTextField", "verticalScrollbar.position", "")]
        #endregion
        public void GetAttributeChildPathTest(string type, string name, string expected)
        {
            // Arrange: a control; table controls get a vertical scroll bar.
            var node = NewControl(type);
            if (type == "GuiTableControl")
            {
                var scrollbar = new FakeSapGuiNode($"{node.Id}/scrollbar", "GuiScrollbar").With("Position", 12).With("Maximum", 199);
                node.Properties["VerticalScrollbar"] = scrollbar;
            }

            // Act: read the dotted attribute.
            var value = SapControlInteractions.GetAttribute(node, name);

            // Assert: the scroll bar value, or empty when the control has no scroll bar.
            Assert.AreEqual(expected, value);
        }

        [TestMethod(DisplayName = "Verify that attribute reads use the whitelist, format values, and return empty for unknown names.")]
        #region *** Data Set ***
        [DataRow("text", "Hello")]
        [DataRow("Tooltip", "Greeting")]
        [DataRow("changeable", "true")]
        [DataRow("left", "15")]
        [DataRow("handle", "FF")]
        [DataRow("iconName", "")]
        [DataRow("password", "")]
        [DataRow("", "")]
        #endregion
        public void GetAttributeTest(string name, string expected)
        {
            // Arrange: a text field with display and geometry properties.
            var node = NewControl("GuiTextField");
            node.Properties["Text"] = "Hello";
            node.Properties["Tooltip"] = "Greeting";
            node.Properties["ScreenLeft"] = 15;
            node.Properties["Handle"] = 255;

            // Act: read the attribute.
            var value = SapControlInteractions.GetAttribute(node, name);

            // Assert: the formatted value matches.
            Assert.AreEqual(expected, value);
        }

        // Builds a leaf control of the given type below a minimal session.
        private static FakeSapGuiNode NewControl(string type)
        {
            var application = FakeSapTree.NewApplication();
            var userArea = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]/usr");

            return FakeSapTree.AddControl(userArea, name: "ctl", type);
        }
    }
}
