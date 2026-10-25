using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("Inspector")]
    [TestCategory("UnitTest")]
    public class DriverInspectorSourceTests
    {
        // Holds the identifier of the user area every fake session has.
        private const string UserAreaId = "/app/con[0]/ses[0]/wnd[0]/usr";

        [TestMethod(DisplayName = "Verify that the driver source builds the tree and the details from the page source and the driver endpoints.")]
        public async Task TreeAndSnapshotTestAsync()
        {
            // Arrange: a session with a button and a password field, attached through the in-process driver.
            await using var fixture = new Fixture();
            using var source = await fixture.NewSourceAsync(isOwner: true);

            // Act: load the root, its windows, and the snapshots of the button and the password field.
            var root = await source.GetRootAsync();
            var windows = await source.GetChildrenAsync(root.Id);
            var button = await source.GetSnapshotAsync($"{UserAreaId}/btnOK");
            var password = await source.GetSnapshotAsync($"{UserAreaId}/pwdCODE");
            var missing = await source.GetSnapshotAsync($"{UserAreaId}/nothing");

            // Assert: the session is the root, and the snapshot carries identity, geometry, ancestry, and masking.
            Assert.AreEqual("/app/con[0]/ses[0]", root.Id);
            Assert.AreEqual("GuiSession", root.Type);
            Assert.AreEqual("/app/con[0]/ses[0]/wnd[0]", windows.First().Id);
            Assert.AreEqual("OK", button.Properties.Single(i => i.Name == "Text").Value);
            Assert.AreEqual("Continue", button.Properties.Single(i => i.Name == "Tooltip").Value);
            Assert.AreEqual(100, button.Rectangle.Left);
            Assert.AreEqual(220, button.Rectangle.Bottom);
            Assert.AreEqual("/app/con[0]/ses[0]", button.SessionId);
            Assert.AreEqual("/app/con[0]/ses[0]/wnd[0]", button.WindowId);
            var expectedAncestors = new[] { "/app/con[0]/ses[0]", "/app/con[0]/ses[0]/wnd[0]", UserAreaId };
            Assert.AreSequenceEqual(expectedAncestors, button.AncestorIds.ToArray());
            Assert.AreEqual(PropertyCatalog.MaskedValue, password.Properties.Single(i => i.Name == "Text").Value);
            Assert.IsNull(missing);
        }

        [TestMethod(DisplayName = "Verify that driver locators are verified through the driver and that the tester returns geometry.")]
        public async Task LocatorsTestAsync()
        {
            // Arrange: the default fixture session.
            await using var fixture = new Fixture();
            using var source = await fixture.NewSourceAsync(isOwner: true);

            // Act: generate the button's locators and test a locator that matches every button.
            var locators = await source.GenerateLocatorsAsync($"{UserAreaId}/btnOK");
            var matches = await source.FindByLocatorAsync("/app/con[0]/ses[0]", new LocationStrategyModel { Using = LocationStrategyModel.Xpath, Value = "//GuiButton" });

            // Assert: the relative id and text candidates are unique; the tester resolves identity and geometry.
            var relativeId = locators.Single(i => i.Strategy == LocationStrategyModel.SapId);
            Assert.AreEqual("wnd[0]/usr/btnOK", relativeId.Value);
            Assert.IsTrue(relativeId.IsUnique);
            Assert.IsTrue(locators.Single(i => i.Value == "//GuiButton[@text='OK']").IsUnique);
            Assert.HasCount(1, matches);
            Assert.AreEqual($"{UserAreaId}/btnOK", matches[0].Id);
            Assert.AreEqual(150, matches[0].Rectangle.Right);
            Assert.AreEqual("btnOK", matches[0].Summary.Name);
        }

        [TestMethod(DisplayName = "Verify that hover hit tests and quick actions go through the driver endpoints.")]
        public async Task HitTestAndActionsTestAsync()
        {
            // Arrange: findByPosition reports the button with a grid cell for one point only.
            await using var fixture = new Fixture();
            fixture.Session.Methods["findByPosition"] = arguments => (int)arguments[0] == 110
                ? new[] { $"{UserAreaId}/btnOK", "Cell(1,A)" }
                : Array.Empty<string>();
            using var source = await fixture.NewSourceAsync(isOwner: true);

            // Act: hit-test on and off the button, click it, and write the user field.
            var hit = await source.HitTestAsync(new InspectorPoint(110, 205, WindowHandle: 0));
            var miss = await source.HitTestAsync(new InspectorPoint(1, 1, WindowHandle: 0));
            await source.InvokeActionAsync($"{UserAreaId}/btnOK", ActionCatalog.Click, text: string.Empty);
            await source.InvokeActionAsync($"{UserAreaId}/txtUSER", ActionCatalog.SetText, text: "DEVELOPER");

            // Assert: the hit carries identity and inner object, and the actions reached SAP GUI.
            Assert.AreEqual($"{UserAreaId}/btnOK", hit.Id);
            Assert.AreEqual("Cell(1,A)", hit.InnerObject);
            Assert.AreEqual("btnOK", hit.Summary.Name);
            Assert.IsNull(miss);
            Assert.Contains("press()", fixture.Button.Invocations);
            Assert.AreEqual("DEVELOPER", fixture.User.Properties["Text"]);
        }

        [TestMethod(DisplayName = "Verify that desktop samples count only over the session's own windows.")]
        public async Task DesktopWindowFilterTestAsync()
        {
            // Arrange: findByPosition reports the button everywhere.
            await using var fixture = new Fixture();
            fixture.Session.Methods["findByPosition"] = _ => new[] { $"{UserAreaId}/btnOK", string.Empty };
            using var source = await fixture.NewSourceAsync(isOwner: true);

            // Act: sample over the session's main window (handle 0x1A2B) and over a foreign window.
            var own = await source.HitTestAsync(new InspectorPoint(110, 205, WindowHandle: 0x1A2B));
            var foreign = await source.HitTestAsync(new InspectorPoint(110, 205, WindowHandle: 0x9999));

            // Assert: the localhost driver resolves its own window and ignores the foreign one.
            Assert.IsTrue(source.Features.HasDesktopOverlay);
            Assert.AreEqual($"{UserAreaId}/btnOK", own.Id);
            Assert.IsNull(foreign);
        }

        [TestMethod(DisplayName = "Verify that the screen capture returns the PNG with the current window's identity and geometry.")]
        public async Task ScreenCaptureTestAsync()
        {
            // Arrange: HardCopy writes a minimal PNG signature.
            await using var fixture = new Fixture();
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
            fixture.Window.Methods["HardCopy"] = arguments =>
            {
                File.WriteAllBytes((string)arguments[0], pngBytes);
                return arguments[0];
            };
            using var source = await fixture.NewSourceAsync(isOwner: true);

            // Act: capture the screen.
            var capture = await source.GetScreenCaptureAsync();

            // Assert: the bytes, the main window, and its rectangle are returned.
            Assert.AreSequenceEqual(pngBytes, capture.Image);
            Assert.AreEqual("/app/con[0]/ses[0]/wnd[0]", capture.WindowId);
            Assert.AreEqual(10, capture.WindowRect.X);
            Assert.AreEqual(800, capture.WindowRect.Width);
        }

        [TestMethod(DisplayName = "Verify that the driver source reports its limits and refuses members the driver does not offer.")]
        public async Task UnsupportedFeaturesTestAsync()
        {
            // Arrange: the default fixture session.
            await using var fixture = new Fixture();
            using var source = await fixture.NewSourceAsync(isOwner: true);

            // Act: read the features and the actions for a button and a text field.
            var buttonActions = source.GetActions("GuiButton", string.Empty);
            var fieldActions = source.GetActions("GuiTextField", string.Empty);

            // Assert: local-only features are off, focus is not offered, and the invoker is refused.
            Assert.IsFalse(source.Features.CanInvokeMember);
            Assert.IsFalse(source.Features.CanFollowFocus);
            Assert.IsFalse(source.Features.CanVisualize);
            Assert.IsTrue(source.Features.HasScreenCanvas);
            Assert.DoesNotContain(i => i.Id == ActionCatalog.Focus, buttonActions);
            Assert.Contains(i => i.Id == ActionCatalog.SetText, fieldActions);
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => source.InvokeMemberAsync($"{UserAreaId}/btnOK", new MemberInvocation { Name = "press" }));
            Assert.IsFalse(await source.SetVisualizedAsync($"{UserAreaId}/btnOK", "Cell(1,A)", isOn: true));
        }

        [TestMethod(DisplayName = "Verify that disconnecting deletes owned sessions and leaves attached sessions open.")]
        public async Task DisconnectOwnershipTestAsync()
        {
            // Arrange: one owned source, and one attached source over another created session.
            await using var fixture = new Fixture();
            var owned = await fixture.NewSourceAsync(isOwner: true);
            var ownedId = owned.SessionId;
            await owned.DisconnectAsync();
            var attached = await fixture.NewSourceAsync(isOwner: false);
            var attachedId = attached.SessionId;

            // Act: disconnect the attached source and read the driver's sessions.
            await attached.DisconnectAsync();
            using var client = TestDrivers.NewClient(fixture.Factory);
            var status = await client.GetStatusAsync();

            // Assert: only the attached session is still open.
            Assert.DoesNotContain(ownedId, status.Sessions.ToList());
            Assert.Contains(attachedId, status.Sessions.ToList());
        }

        [TestMethod(DisplayName = "Verify that driver errors keep their W3C code and that an unreachable driver names its address.")]
        public async Task ClientErrorsTestAsync()
        {
            // Arrange: a running driver, and an address where nothing listens.
            await using var fixture = new Fixture();
            using var client = TestDrivers.NewClient(fixture.Factory);
            using var unreachable = new SapDriverClient("http://127.0.0.1:1/wd/hub");

            // Act: read the source of an unknown session, and the status of the unreachable driver.
            var invalid = await Assert.ThrowsExactlyAsync<SapDriverException>(() => client.GetSourceAsync("missing"));
            var offline = await Assert.ThrowsExactlyAsync<SapDriverException>(unreachable.GetStatusAsync);

            // Assert: the driver's code is kept, and the offline message names the address.
            Assert.AreEqual(WebDriverErrorCodes.InvalidSessionId, invalid.Error);
            Assert.AreEqual(WebDriverErrorCodes.UnknownError, offline.Error);
            Assert.Contains("127.0.0.1:1", offline.Message);
        }

        #region *** Data Set ***
        [DataRow("http://localhost:5556/wd/hub", "http://localhost:5556/wd/hub/")]
        [DataRow("https://sap-box:5556/", "https://sap-box:5556/")]
        #endregion
        [TestMethod(DisplayName = "Verify that driver addresses get a trailing slash so relative paths keep the hub prefix.")]
        public void ConvertToBaseAddressTest(string driverUrl, string expected)
        {
            // Act: normalize the address.
            var address = SapDriverClient.ConvertToBaseAddress(driverUrl);

            // Assert: the trailing slash is present exactly once.
            Assert.AreEqual(expected, address.AbsoluteUri);
        }

        #region *** Data Set ***
        [DataRow("localhost:5556")]
        [DataRow("ftp://sap-box/")]
        [DataRow("")]
        #endregion
        [TestMethod(DisplayName = "Verify that addresses which are not absolute HTTP URLs are rejected.")]
        public void ConvertToBaseAddressInvalidTest(string driverUrl)
        {
            // Act and assert: the address is rejected with an argument error.
            Assert.ThrowsExactly<ArgumentException>(() => SapDriverClient.ConvertToBaseAddress(driverUrl));
        }

        // Owns a fake SAP tree with a button, a user field, and a password field, served by an in-process driver.
        private sealed class Fixture : IAsyncDisposable
        {
            public Fixture()
            {
                // Build the controls the tests inspect and act on.
                var application = FakeSapTree.NewApplication();
                var userArea = (FakeSapGuiNode)application.FindById(UserAreaId);
                Session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
                Window = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]");
                Button = FakeSapTree.AddControl(userArea, name: "btnOK", type: "GuiButton")
                    .With("Text", "OK")
                    .With("Tooltip", "Continue")
                    .With("ScreenLeft", 100)
                    .With("ScreenTop", 200)
                    .With("Width", 50)
                    .With("Height", 20);
                User = FakeSapTree.AddControl(userArea, name: "txtUSER", type: "GuiTextField");
                FakeSapTree.AddControl(userArea, name: "pwdCODE", type: "GuiPasswordField").With("Text", "TopSecret1");
                Factory = TestDrivers.NewFactory(application);
            }

            public FakeSapGuiNode Button { get; }

            public WebApplicationFactory<Program> Factory { get; }

            public FakeSapGuiNode Session { get; }

            public FakeSapGuiNode User { get; }

            public FakeSapGuiNode Window { get; }

            public async ValueTask DisposeAsync()
            {
                // Stop the server and its dispatcher thread.
                await Factory.DisposeAsync();
            }

            public async Task<DriverInspectorSource> NewSourceAsync(bool isOwner)
            {
                // Create a WebDriver session through the driver, then wrap it like the dialog does.
                var client = TestDrivers.NewClient(Factory);
                var sessionId = await client.NewSessionAsync(new JsonObject());

                return new DriverInspectorSource(client, sessionId, isOwner);
            }
        }
    }
}
