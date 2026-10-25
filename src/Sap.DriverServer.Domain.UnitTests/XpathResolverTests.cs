using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("XpathResolver")]
    [TestCategory("UnitTest")]
    public class XpathResolverTests
    {
        // Holds the absolute identifier of the main window's user area in the fixture tree.
        private const string UserAreaId = "/app/con[0]/ses[0]/wnd[0]/usr";

        [TestMethod(DisplayName = "Verify that the live-tree resolver returns exactly the document XPath result, in document order.")]
        #region *** Data Set ***
        [DataRow("//GuiButton")]
        [DataRow("/GuiSession/GuiMainWindow/GuiUserArea/GuiButton")]
        [DataRow("/GuiSession/GuiMainWindow/GuiUserArea/GuiButton[2]")]
        [DataRow("//GuiContainer/GuiButton")]
        [DataRow("//GuiContainer//GuiButton")]
        [DataRow("//GuiButton[1]")]
        [DataRow("//GuiButton[@text='OK']")]
        [DataRow("//GuiButton[@text=\"OK\"][1]")]
        [DataRow("//GuiButton[2][@text='OK']")]
        [DataRow("//GuiUserArea//GuiButton[@tooltip='Next (F8)']")]
        [DataRow("//*[@name='btnOK']")]
        [DataRow("//GuiTextField[@changeable='true']")]
        [DataRow("//GuiTextField[@changeable = 'false']")]
        [DataRow("//GuiPasswordField[@text='secret']")]
        [DataRow("//GuiShell[@subType='GridView']")]
        [DataRow("//GuiLabel[@subType='GridView']")]
        [DataRow("//GuiButton[@text='']")]
        [DataRow("/*//GuiButton")]
        [DataRow(".//GuiButton")]
        [DataRow("./GuiSession/*")]
        [DataRow("//GuiSession")]
        [DataRow("/GuiMainWindow")]
        [DataRow("//*")]
        [DataRow("//*[3]")]
        [DataRow("//GuiButton[@id='/app/con[0]/ses[0]/wnd[0]/usr/cntA/btnOK']")]
        [DataRow("//*[@id='/app/con[0]/ses[0]/wnd[0]/usr']")]
        [DataRow("//GuiLabel[@id='/app/con[0]/ses[0]/wnd[0]/usr/cntA/btnOK']")]
        [DataRow("//GuiButton[@id='wnd[0]/usr/cntA/btnOK']")]
        #endregion
        public void FindElementsMatchesDocumentTest(string expression)
        {
            // Arrange: a nested screen, and the document evaluation of the same expression as the reference result.
            var (application, session) = NewNestedScreen();
            var expected = DocumentObjectModelFactory
                .NewDocument(session)
                .XPathSelectElements(expression)
                .Select(i => (string)i.Attribute("id"))
                .ToList();

            // Act: parse and resolve the expression on the live tree.
            var steps = XpathResolver.ConvertToSteps(expression);
            var actual = XpathResolver.FindElements(application, session, steps).Select(i => i.SapId).ToList();

            // Assert: the expression is in the grammar and both paths return the same identifiers in the same order.
            Assert.IsNotNull(steps);
            Assert.AreSequenceEqual(expected, actual);
        }

        [TestMethod(DisplayName = "Verify that expressions outside the resolver grammar are left to the document path.")]
        #region *** Data Set ***
        [DataRow("")]
        [DataRow("GuiButton")]
        [DataRow("//GuiButton[contains(@text,'O')]")]
        [DataRow("//GuiButton[@text='OK' and @name='btnOK']")]
        [DataRow("//GuiButton[last()]")]
        [DataRow("//GuiButton[0]")]
        [DataRow("//GuiButton[@icon='X']")]
        [DataRow("//GuiButton/@id")]
        [DataRow("(//GuiButton)[1]")]
        [DataRow("//GuiButton | //GuiLabel")]
        [DataRow("//GuiButton[@text='OK")]
        [DataRow("//GuiButton/..")]
        [DataRow("//ancestor::GuiUserArea")]
        [DataRow("//GuiButton[@text=concat('O','K')]")]
        #endregion
        public void ConvertToStepsUnsupportedTest(string expression)
        {
            // Act: parse the expression.
            var steps = XpathResolver.ConvertToSteps(expression);

            // Assert: the resolver declines it.
            Assert.IsNull(steps);
        }

        [TestMethod(DisplayName = "Verify that an absolute child path only reads the Children of objects on the path.")]
        public void FindElementsChildPathReadsOnlyPathTest()
        {
            // Arrange: a nested screen with a sibling container that is not on the path.
            var (application, session) = NewNestedScreen();
            var containerB = (FakeSapGuiNode)session.FindById("wnd[0]/usr/cntB");
            var statusBar = (FakeSapGuiNode)session.FindById("wnd[0]/sbar");
            var steps = XpathResolver.ConvertToSteps("/GuiSession/GuiMainWindow/GuiUserArea/GuiContainer[1]/GuiButton");

            // Act: resolve the path.
            var matches = XpathResolver.FindElements(application, session, steps);

            // Assert: the buttons of the first container are found and off-path subtrees were never walked.
            Assert.HasCount(2, matches);
            Assert.AreEqual(0, containerB.ChildrenReads);
            Assert.AreEqual(0, statusBar.ChildrenReads);
        }

        [TestMethod(DisplayName = "Verify that //Type[@id='…'] resolves through findById without walking the tree.")]
        public void FindElementsIdShortcutTest()
        {
            // Arrange: a nested screen and an identifier predicate.
            var (application, session) = NewNestedScreen();
            var steps = XpathResolver.ConvertToSteps($"//GuiButton[@id='{UserAreaId}/cntB/btnNEXT']");

            // Act: resolve the expression.
            var matches = XpathResolver.FindElements(application, session, steps);

            // Assert: the control is found and no Children collection anywhere in the tree was read.
            Assert.HasCount(1, matches);
            Assert.AreEqual($"{UserAreaId}/cntB/btnNEXT", matches[0].SapId);
            Assert.IsTrue(application.GetSelfAndDescendants().All(i => i.ChildrenReads == 0));
        }

        [TestMethod(DisplayName = "Verify that the resolver reads only the properties its predicates need, never text for a name predicate.")]
        public void FindElementsReadsOnlyNeededPropertiesTest()
        {
            // Arrange: a nested screen and a name predicate.
            var (application, session) = NewNestedScreen();
            var steps = XpathResolver.ConvertToSteps("//GuiButton[@name='btnOK']");

            // Act: resolve the expression.
            XpathResolver.FindElements(application, session, steps);

            // Assert: no visited object had its Text, Tooltip, or Changeable property read.
            var displayReads = application
                .GetSelfAndDescendants()
                .SelectMany(i => i.PropertyReads)
                .Where(i => i is "Text" or "Tooltip" or "Changeable");
            Assert.IsEmpty(displayReads);
        }

        [TestMethod(DisplayName = "Verify that element-relative resolution is scoped to the parent subtree like the document path.")]
        public void FindElementsRelativeContextTest()
        {
            // Arrange: use the second container as the search context.
            var (application, session) = NewNestedScreen();
            var context = session.FindById("wnd[0]/usr/cntB");
            var outside = XpathResolver.ConvertToSteps($"//GuiButton[@id='{UserAreaId}/cntA/btnOK']");
            var inside = XpathResolver.ConvertToSteps("//GuiButton");

            // Act: resolve an identifier outside the context and every button inside it.
            var outsideMatches = XpathResolver.FindElements(application, context, outside);
            var insideMatches = XpathResolver.FindElements(application, context, inside);

            // Assert: the outside control is excluded and only the context's own button is found.
            Assert.IsEmpty(outsideMatches);
            var expected = new[] { $"{UserAreaId}/cntB/btnNEXT" };
            Assert.AreSequenceEqual(expected, insideMatches.Select(i => i.SapId));
        }

        [TestMethod(DisplayName = "Verify that the locator uses the resolver for XPath and tag name strategies without reading display text.")]
        public void ElementLocatorUsesResolverTest()
        {
            // Arrange: a nested screen and a tag name locator.
            var (application, session) = NewNestedScreen();
            var locator = new LocationStrategyModel { Using = LocationStrategyModel.TagName, Value = "GuiButton" };

            // Act: locate through the public locator entry point.
            var matches = ElementLocator.FindElements(application, session, locator);

            // Assert: every button is found and no Text property was read, which the document path always reads.
            Assert.HasCount(3, matches);
            Assert.DoesNotContain("Text", application.GetSelfAndDescendants().SelectMany(i => i.PropertyReads));
        }

        [TestMethod(DisplayName = "Verify that the resolver applies the document depth limit to deeply nested trees.")]
        public void FindElementsDepthLimitTest()
        {
            // Arrange: a chain of nested containers deeper than the document limit, with a button at each end.
            var (application, session) = NewNestedScreen();
            var container = (FakeSapGuiNode)session.FindById("wnd[0]/usr");
            for (var level = 0; level < DocumentObjectModelFactory.MaximumDepth + 5; level++)
            {
                container = container.Add(new FakeSapGuiNode($"{container.Id}/c{level}", "GuiContainer"));
            }

            FakeSapTree.AddControl(container, name: "btnDEEP", type: "GuiButton");
            var expected = DocumentObjectModelFactory
                .NewDocument(session)
                .XPathSelectElements("//GuiButton")
                .Select(i => (string)i.Attribute("id"))
                .ToList();

            // Act: resolve every button on the live tree.
            var actual = XpathResolver
                .FindElements(application, session, XpathResolver.ConvertToSteps("//GuiButton"))
                .Select(i => i.SapId)
                .ToList();

            // Assert: the button beyond the limit is excluded by both paths.
            Assert.AreSequenceEqual(expected, actual);
            Assert.DoesNotContain($"{container.Id}/btnDEEP", actual);
        }

        // Builds a session whose user area holds two containers with buttons, fields, a password field, a label, and a grid.
        private static (FakeSapGuiNode Application, FakeSapGuiNode Session) NewNestedScreen()
        {
            var application = FakeSapTree.NewApplication();
            var session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
            var userArea = (FakeSapGuiNode)session.FindById("wnd[0]/usr");

            var containerA = userArea.Add(new FakeSapGuiNode($"{userArea.Id}/cntA", "GuiContainer"));
            containerA.With("Name", "cntA");
            FakeSapTree.AddControl(containerA, name: "btnOK", type: "GuiButton").With("Text", "OK");
            FakeSapTree.AddControl(containerA, name: "btnCANCEL", type: "GuiButton").With("Text", "OK").With("Tooltip", "Cancel");
            FakeSapTree.AddControl(containerA, name: "txtUSER", type: "GuiTextField").With("Changeable", false);

            var containerB = userArea.Add(new FakeSapGuiNode($"{userArea.Id}/cntB", "GuiContainer"));
            containerB.With("Name", "cntB");
            FakeSapTree.AddControl(containerB, name: "btnNEXT", type: "GuiButton").With("Text", "Next").With("Tooltip", "Next (F8)");
            FakeSapTree.AddControl(containerB, name: "pwdCODE", type: "GuiPasswordField").With("Text", "secret");

            FakeSapTree.AddControl(userArea, name: "txtCLIENT", type: "GuiTextField").With("Text", "100");
            FakeSapTree.AddControl(userArea, name: "lblTITLE", type: "GuiLabel").With("Text", "Title");
            FakeSapTree.AddControl(userArea, name: "shellcont/shell", type: "GuiShell").With("SubType", "GridView");

            return (application, session);
        }
    }
}
