using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("ElementLocator")]
    [TestCategory("UnitTest")]
    public class ElementLocatorTests
    {
        [TestMethod(DisplayName = "Verify that each supported strategy resolves the expected SAP identifiers in document order.")]
        #region *** Data Set ***
        [DataRow(LocationStrategyModel.Xpath, "//GuiButton[@tooltip='Save (Ctrl+S)']", "/app/con[0]/ses[0]/wnd[0]/usr/btnSAVE")]
        [DataRow(LocationStrategyModel.Xpath, "//GuiTextField[@name='RSYST-BNAME']", "/app/con[0]/ses[0]/wnd[0]/usr/txtRSYST-BNAME")]
        [DataRow(LocationStrategyModel.SapId, "/app/con[0]/ses[0]/wnd[0]/usr/btnSAVE", "/app/con[0]/ses[0]/wnd[0]/usr/btnSAVE")]
        [DataRow(LocationStrategyModel.SapId, "wnd[0]/usr/txtRSYST-BNAME", "/app/con[0]/ses[0]/wnd[0]/usr/txtRSYST-BNAME")]
        [DataRow(LocationStrategyModel.TagName, "GuiPasswordField", "/app/con[0]/ses[0]/wnd[0]/usr/pwdRSYST-BCODE")]
        [DataRow(LocationStrategyModel.CssSelector, "#wnd[0]/usr/btnSAVE", "/app/con[0]/ses[0]/wnd[0]/usr/btnSAVE")]
        [DataRow(LocationStrategyModel.CssSelector, "[id=\"wnd\\[0\\]\\/usr\\/btnSAVE\"]", "/app/con[0]/ses[0]/wnd[0]/usr/btnSAVE")]
        [DataRow(LocationStrategyModel.CssSelector, "*[name=\"RSYST-BNAME\"]", "/app/con[0]/ses[0]/wnd[0]/usr/txtRSYST-BNAME")]
        [DataRow(LocationStrategyModel.CssSelector, "GuiButton[text='Save']", "/app/con[0]/ses[0]/wnd[0]/usr/btnSAVE")]
        #endregion
        public void FindElementsStrategyTest(string strategy, string value, string expectedId)
        {
            // Arrange: a session with a logon-like screen.
            var (application, session) = NewLogonScreen();
            var locator = new LocationStrategyModel { Using = strategy, Value = value };

            // Act: locate within the session.
            var matches = ElementLocator.FindElements(application, session, locator);

            // Assert: exactly the expected control is found, with its SAP type.
            Assert.HasCount(1, matches);
            Assert.AreEqual(expectedId, matches[0].SapId);
            Assert.IsFalse(string.IsNullOrEmpty(matches[0].Type));
        }

        [TestMethod(DisplayName = "Verify that a tag name locator returns every control of the type in document order.")]
        public void FindElementsTagNameOrderTest()
        {
            // Arrange: a session with two buttons.
            var (application, session) = NewLogonScreen();
            var locator = new LocationStrategyModel { Using = LocationStrategyModel.TagName, Value = "GuiButton" };

            // Act: locate all buttons.
            var ids = ElementLocator.FindElements(application, session, locator).Select(i => i.SapId).ToArray();

            // Assert: both buttons are found in tree order.
            var expectedIds = new[] { "/app/con[0]/ses[0]/wnd[0]/usr/btnSAVE", "/app/con[0]/ses[0]/wnd[0]/usr/btnBACK" };
            Assert.AreSequenceEqual(expectedIds, ids);
        }

        [TestMethod(DisplayName = "Verify that missing identifiers and non-matching XPath return no matches instead of failing.")]
        public void FindElementsNoMatchTest()
        {
            // Arrange: a session with a logon-like screen.
            var (application, session) = NewLogonScreen();

            // Act: search for absent controls through both resolution paths.
            var byId = ElementLocator.FindElements(application, session, new LocationStrategyModel { Using = LocationStrategyModel.SapId, Value = "wnd[0]/usr/btnNONE" });
            var byXpath = ElementLocator.FindElements(application, session, new LocationStrategyModel { Using = LocationStrategyModel.Xpath, Value = "//GuiTree" });

            // Assert: both are empty.
            Assert.IsEmpty(byId);
            Assert.IsEmpty(byXpath);
        }

        [TestMethod(DisplayName = "Verify that element-relative searches only return descendants of the parent.")]
        public void FindElementsRelativeTest()
        {
            // Arrange: use the user area as the parent context.
            var (application, session) = NewLogonScreen();
            var userArea = session.FindById("wnd[0]/usr");

            // Act: search for buttons and for the parent's own type.
            var buttons = ElementLocator.FindElements(application, userArea, new LocationStrategyModel { Using = LocationStrategyModel.TagName, Value = "GuiButton" });
            var self = ElementLocator.FindElements(application, userArea, new LocationStrategyModel { Using = LocationStrategyModel.TagName, Value = "GuiUserArea" });

            // Assert: descendants are found and the parent itself is excluded.
            Assert.HasCount(2, buttons);
            Assert.IsEmpty(self);
        }

        [TestMethod(DisplayName = "Verify that unsupported strategies are invalid arguments and malformed selectors are invalid selectors.")]
        #region *** Data Set ***
        [DataRow("link text", "Save", WebDriverErrorCodes.InvalidArgument)]
        [DataRow(LocationStrategyModel.Xpath, "//GuiButton[", WebDriverErrorCodes.InvalidSelector)]
        [DataRow(LocationStrategyModel.Xpath, "//GuiButton/@id", WebDriverErrorCodes.InvalidSelector)]
        [DataRow(LocationStrategyModel.CssSelector, "div > span", WebDriverErrorCodes.InvalidSelector)]
        #endregion
        public void FindElementsInvalidLocatorTest(string strategy, string value, string expectedError)
        {
            // Arrange: a session with a logon-like screen.
            var (application, session) = NewLogonScreen();
            var locator = new LocationStrategyModel { Using = strategy, Value = value };

            // Act: evaluate the bad locator.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => ElementLocator.FindElements(application, session, locator));

            // Assert: the failure is a 400 with the expected W3C code.
            Assert.AreEqual(expectedError, exception.Error);
            Assert.AreEqual(400, exception.StatusCode);
        }

        [TestMethod(DisplayName = "Verify that ConvertToXpathLiteral quotes values containing either or both quote kinds.")]
        #region *** Data Set ***
        [DataRow("plain", "'plain'")]
        [DataRow("it's", "\"it's\"")]
        [DataRow("a'b\"c", "concat('a', \"'\", 'b\"c')")]
        #endregion
        public void ConvertToXpathLiteralTest(string value, string expected)
        {
            // Act: convert the value.
            var literal = ElementLocator.ConvertToXpathLiteral(value);

            // Assert: the literal matches the expected XPath expression.
            Assert.AreEqual(expected, literal);
        }

        // Builds one session whose main window shows a logon-like screen with fields and buttons.
        private static (FakeSapGuiNode Application, FakeSapGuiNode Session) NewLogonScreen()
        {
            var application = FakeSapTree.NewApplication();
            var session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
            var userArea = (FakeSapGuiNode)session.FindById("wnd[0]/usr");

            var user = FakeSapTree.AddControl(userArea, name: "txtRSYST-BNAME", type: "GuiTextField");
            user.Properties["Name"] = "RSYST-BNAME";
            FakeSapTree.AddControl(userArea, name: "pwdRSYST-BCODE", type: "GuiPasswordField").With("Text", "secret");
            var save = FakeSapTree.AddControl(userArea, name: "btnSAVE", type: "GuiButton").With("Text", "Save");
            save.Properties["Tooltip"] = "Save (Ctrl+S)";
            FakeSapTree.AddControl(userArea, name: "btnBACK", type: "GuiButton").With("Text", "Back");

            return (application, session);
        }
    }
}
