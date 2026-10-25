using System;
using System.Linq;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("InspectorService")]
    [TestCategory("UnitTest")]
    public class InspectorServiceTests
    {
        // Holds the absolute identifier of the fixture session.
        private const string SessionId = "/app/con[0]/ses[0]";

        // Holds the absolute identifier of the fixture user area.
        private const string UserAreaId = "/app/con[0]/ses[0]/wnd[0]/usr";

        // Holds the main window handle the fake tree assigns to ses[0] (0x1A2B).
        private const long MainWindowHandle = 0x1A2B;

        [TestMethod(DisplayName = "Verify that HitTest maps the window to its session and resolves the control through findByPosition.")]
        public void HitTestResolvesControlTest()
        {
            // Arrange: a session whose findByPosition reports the OK button and a grid cell for two positions.
            var fixture = new Fixture();
            fixture.Session.Methods["findByPosition"] = arguments => (int)arguments[0] == 100
                ? new[] { $"{UserAreaId}/btnOK", string.Empty }
                : new[] { "wnd[0]/usr/grid", "Cell(2,PRICE)" };
            using var host = TestHosts.NewHost(new FakeSapGuiEngine(fixture.Application));
            var service = new InspectorService(host);

            // Act: hit-test an absolute result, a relative result with an inner object, and a non-SAP window.
            var button = service.HitTest(new InspectorPoint(100, 200, MainWindowHandle));
            var cell = service.HitTest(new InspectorPoint(300, 400, MainWindowHandle));
            var miss = service.HitTest(new InspectorPoint(100, 200, WindowHandle: 0x9999));

            // Assert: both hits resolve with geometry and inner object; the foreign window is a miss.
            Assert.AreEqual($"{UserAreaId}/btnOK", button.Id);
            Assert.AreEqual("GuiButton", button.Summary.Type);
            Assert.AreEqual(150, button.Rectangle.Right);
            Assert.AreEqual($"{UserAreaId}/grid", cell.Id);
            Assert.AreEqual("Cell(2,PRICE)", cell.InnerObject);
            Assert.IsNull(miss);
            Assert.Contains("findByPosition(100,200,False)", fixture.Session.Invocations);
        }

        [TestMethod(DisplayName = "Verify that the window cache is rebuilt on a miss at most once per refresh interval.")]
        public void HitTestWindowCacheThrottleTest()
        {
            // Arrange: a controllable clock and a session that reports no control.
            var fixture = new Fixture();
            var now = 0L;
            fixture.Session.Methods["findByPosition"] = _ => Array.Empty<string>();
            using var host = TestHosts.NewHost(new FakeSapGuiEngine(fixture.Application));
            var service = new InspectorService(host, clock: () => now);
            var foreign = new InspectorPoint(1, 1, WindowHandle: 0x9999);

            // Act: miss twice within the interval, then once after it.
            service.HitTest(foreign);
            var afterFirst = fixture.Application.ChildrenReads;
            now += 10;
            service.HitTest(foreign);
            var afterSecond = fixture.Application.ChildrenReads;
            now += InspectorService.WindowRefreshMilliseconds;
            service.HitTest(foreign);

            // Assert: the first miss rebuilt the cache, the second did not, and the third rebuilt it again.
            Assert.IsGreaterThan(0, afterFirst);
            Assert.AreEqual(afterFirst, afterSecond);
            Assert.IsGreaterThan(afterSecond, fixture.Application.ChildrenReads);
        }

        [TestMethod(DisplayName = "Verify that GetSnapshot returns properties, ancestors, session and window, and never reads password text.")]
        public void GetSnapshotTest()
        {
            // Arrange: a filled password field.
            var fixture = new Fixture();
            using var host = TestHosts.NewHost(new FakeSapGuiEngine(fixture.Application));
            var service = new InspectorService(host);

            // Act: snapshot the password field and the session.
            var password = service.GetSnapshot($"{UserAreaId}/pwdCODE");
            var session = service.GetSnapshot(SessionId);

            // Assert: identity, path, and masking are correct, and the password text was never requested.
            Assert.AreEqual("GuiPasswordField", password.Summary.Type);
            Assert.AreSequenceEqual(new[] { "/app", "/app/con[0]", SessionId, $"{SessionId}/wnd[0]", UserAreaId }, password.AncestorIds);
            Assert.AreEqual(SessionId, password.SessionId);
            Assert.AreEqual($"{SessionId}/wnd[0]", password.WindowId);
            Assert.AreEqual(PropertyCatalog.MaskedValue, password.Properties.Single(i => i.Name == "Text").Value);
            Assert.DoesNotContain("Text", fixture.Password.PropertyReads);

            // Assert: session information is shown without the logged-on user.
            Assert.AreEqual("DEV", session.Properties.Single(i => i.Name == "Info.SystemName").Value);
            Assert.DoesNotContain("SECRET-USER", session.Properties.Select(i => i.Value));
        }

        [TestMethod(DisplayName = "Verify that tree navigation returns parents, children, and siblings, and null at the edges.")]
        #region *** Data Set ***
        [DataRow("/app/con[0]/ses[0]/wnd[0]/usr/btnOK", InspectorNavigation.Parent, "/app/con[0]/ses[0]/wnd[0]/usr")]
        [DataRow("/app/con[0]/ses[0]/wnd[0]/usr", InspectorNavigation.FirstChild, "/app/con[0]/ses[0]/wnd[0]/usr/btnOK")]
        [DataRow("/app/con[0]/ses[0]/wnd[0]/usr", InspectorNavigation.LastChild, "/app/con[0]/ses[0]/wnd[0]/usr/grid")]
        [DataRow("/app/con[0]/ses[0]/wnd[0]/usr/btnOK", InspectorNavigation.NextSibling, "/app/con[0]/ses[0]/wnd[0]/usr/txtUSER")]
        [DataRow("/app/con[0]/ses[0]/wnd[0]/usr/txtUSER", InspectorNavigation.PreviousSibling, "/app/con[0]/ses[0]/wnd[0]/usr/btnOK")]
        [DataRow("/app/con[0]/ses[0]/wnd[0]/usr/btnOK", InspectorNavigation.PreviousSibling, null)]
        [DataRow("/app/con[0]/ses[0]/wnd[0]/usr/btnOK", InspectorNavigation.FirstChild, null)]
        [DataRow("/app", InspectorNavigation.FirstChild, "/app/con[0]")]
        #endregion
        public void GetRelativeIdTest(string id, string direction, string expected)
        {
            // Arrange: the standard fixture tree.
            var fixture = new Fixture();
            using var host = TestHosts.NewHost(new FakeSapGuiEngine(fixture.Application));
            var service = new InspectorService(host);

            // Act: navigate.
            var relative = service.GetRelativeId(id, direction);

            // Assert: the expected relative, or null at the edge of the tree.
            Assert.AreEqual(expected, relative);
        }

        [TestMethod(DisplayName = "Verify that generated locators are verified against the session and flag non-unique candidates.")]
        public void GenerateLocatorsTest()
        {
            // Arrange: two buttons that share their text, so a text locator is ambiguous.
            var fixture = new Fixture();
            FakeSapTree.AddControl(fixture.UserArea, name: "btnOK2", type: "GuiButton").With("Text", "OK");
            using var host = TestHosts.NewHost(new FakeSapGuiEngine(fixture.Application));
            var service = new InspectorService(host);

            // Act: generate locators for the first button.
            var locators = service.GenerateLocators($"{UserAreaId}/btnOK");

            // Assert: the relative id, name, and id XPath are unique; the text XPath matches both buttons.
            var relative = locators.Single(i => i.Strategy == LocationStrategyModel.SapId);
            Assert.AreEqual("wnd[0]/usr/btnOK", relative.Value);
            Assert.IsTrue(relative.IsUnique);
            Assert.IsTrue(locators.Single(i => i.Value == "//GuiButton[@name='btnOK']").IsUnique);
            var byText = locators.Single(i => i.Value == "//GuiButton[@text='OK']");
            Assert.AreEqual(2, byText.MatchCount);
            Assert.IsFalse(byText.IsUnique);
            Assert.IsTrue(locators.Single(i => i.Value == $"//GuiButton[@id='{UserAreaId}/btnOK']").IsUnique);
        }

        [TestMethod(DisplayName = "Verify that quick actions, member invocation, virtual keys, and the status bar reach SAP GUI as expected.")]
        public void InteractionsTest()
        {
            // Arrange: describe two virtual keys and reject one key, as SAP GUI does for unknown keys.
            var fixture = new Fixture();
            var describeCalls = 0;
            fixture.Session.Methods["GetVKeyDescription"] = arguments =>
            {
                describeCalls++;
                return (int)arguments[0] switch
                {
                    0 => "Enter",
                    3 => "F3",
                    99 => throw new COMException("rejected", unchecked((int)0x80004005)),
                    _ => string.Empty
                };
            };
            using var host = TestHosts.NewHost(new FakeSapGuiEngine(fixture.Application));
            var service = new InspectorService(host);
            var buttonId = $"{UserAreaId}/btnOK";

            // Act: press the button, call a method with arguments, read and write properties, and load the keys twice.
            service.InvokeAction(buttonId, ActionCatalog.Click, text: string.Empty);
            service.InvokeAction($"{UserAreaId}/txtUSER", ActionCatalog.SetText, text: "JDOE");
            var methodResult = service.InvokeMember(buttonId, new MemberInvocation { Arguments = ["a", 2], Kind = MemberInvocation.Method, Name = "doSomething" });
            var getResult = service.InvokeMember(buttonId, new MemberInvocation { Kind = MemberInvocation.GetProperty, Name = "Tooltip" });
            service.InvokeMember(buttonId, new MemberInvocation { Arguments = ["Hi"], Kind = MemberInvocation.SetProperty, Name = "Tooltip" });
            var keys = service.GetVirtualKeys(SessionId);
            service.GetVirtualKeys(SessionId);
            var statusBar = service.GetStatusBar(SessionId);

            // Assert: each call reached the right member, keys are cached, and the status bar is formatted.
            var expectedInvocations = new[] { "press()", "doSomething(a,2)", "set Tooltip=Hi" };
            var expectedKeys = new[] { 0, 3 };
            Assert.AreSequenceEqual(expectedInvocations, fixture.Button.Invocations);
            Assert.Contains("set Text=JDOE", fixture.User.Invocations);
            Assert.AreEqual("(null)", methodResult);
            Assert.AreEqual("Continue", getResult);
            Assert.AreSequenceEqual(expectedKeys, keys.Select(i => i.Key));
            Assert.AreEqual(100, describeCalls);
            Assert.AreEqual("[S 00/344] Saved", statusBar);
        }

        [TestMethod(DisplayName = "Verify that the SAP frame, focus lookup, locator tester, and missing objects behave as documented.")]
        public void FrameFocusAndLocatorTest()
        {
            // Arrange: a grid that accepts Visualize and a focused text field.
            var fixture = new Fixture();
            fixture.Grid.Methods["Visualize"] = _ => true;
            ((FakeSapGuiNode)fixture.Application.FindById($"{SessionId}/wnd[0]")).With("GuiFocus", fixture.User);
            using var host = TestHosts.NewHost(new FakeSapGuiEngine(fixture.Application));
            var service = new InspectorService(host);

            // Act: frame a cell, frame a vanished object, read the focus, and test a locator.
            var isFramed = service.SetVisualized(fixture.Grid.Id, "Cell(2,PRICE)", isOn: true);
            var isGoneFramed = service.SetVisualized($"{UserAreaId}/nothing", string.Empty, isOn: true);
            var focusId = service.GetFocusedId(SessionId);
            var matches = service.FindByLocator(SessionId, new LocationStrategyModel { Using = LocationStrategyModel.TagName, Value = "GuiButton" });

            // Assert: the frame call carries the inner object, missing objects report false, and lookups resolve.
            Assert.IsTrue(isFramed);
            Assert.Contains("Visualize(True,Cell(2,PRICE))", fixture.Grid.Invocations);
            Assert.IsFalse(isGoneFramed);
            Assert.AreEqual(fixture.User.Id, focusId);
            Assert.HasCount(1, matches);
            Assert.AreEqual(fixture.Button.Id, matches[0].Id);
            Assert.ThrowsExactly<SapDriverException>(() => service.InvokeAction($"{UserAreaId}/nothing", ActionCatalog.Click, text: string.Empty));
        }

        // Builds the standard fixture: a session whose user area holds a button, a text field, a password field, and a grid.
        private sealed class Fixture
        {
            public Fixture()
            {
                Application = FakeSapTree.NewApplication();
                Session = (FakeSapGuiNode)Application.FindById(SessionId);
                UserArea = (FakeSapGuiNode)Application.FindById(UserAreaId);
                Button = FakeSapTree.AddControl(UserArea, name: "btnOK", type: "GuiButton")
                    .With("Text", "OK")
                    .With("Tooltip", "Continue")
                    .With("ScreenLeft", 100)
                    .With("ScreenTop", 200)
                    .With("Width", 50)
                    .With("Height", 20);
                User = FakeSapTree.AddControl(UserArea, name: "txtUSER", type: "GuiTextField");
                Password = FakeSapTree.AddControl(UserArea, name: "pwdCODE", type: "GuiPasswordField").With("Text", "TopSecret1");
                Grid = FakeSapTree.AddControl(UserArea, name: "grid", type: "GuiShell").With("SubType", "GridView");
            }

            public FakeSapGuiNode Application { get; }

            public FakeSapGuiNode Button { get; }

            public FakeSapGuiNode Grid { get; }

            public FakeSapGuiNode Password { get; }

            public FakeSapGuiNode Session { get; }

            public FakeSapGuiNode User { get; }

            public FakeSapGuiNode UserArea { get; }
        }
    }
}
