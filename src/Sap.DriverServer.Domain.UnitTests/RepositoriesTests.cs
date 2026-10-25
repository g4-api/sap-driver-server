using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("Repositories")]
    [TestCategory("UnitTest")]
    public class RepositoriesTests
    {
        [TestMethod(DisplayName = "Verify that NewSession attaches to the selected SAP session and reports it in the sap:session capability.")]
        public void NewSessionAttachesTest()
        {
            // Arrange: one open session and options selecting it by system and client.
            using var fixture = new RepositoryFixture(FakeSapTree.NewApplication());
            var request = NewSessionRequest(new Dictionary<string, object> { ["systemName"] = "DEV", ["client"] = "100" });

            // Act: create the session.
            var (statusCode, entity) = fixture.Sessions.NewSession(request);

            // Assert: the session is stored with the SAP path and returned with the W3C envelope.
            Assert.AreEqual(200, statusCode);
            var session = fixture.SessionStore.Values.Single();
            Assert.AreEqual("/app/con[0]/ses[0]", session.SapSessionPath);
            var value = (SessionResponseModel)((WebDriverResponseModel)entity).Value;
            Assert.AreEqual(session.SessionId, value.SessionId);
            Assert.IsTrue(value.Capabilities.ContainsKey(SapCapabilities.Session));
        }

        [TestMethod(DisplayName = "Verify that NewSession rejects a malformed sap:options value with invalid argument.")]
        public void NewSessionInvalidOptionsTest()
        {
            // Arrange: sap:options is a string instead of an object.
            using var fixture = new RepositoryFixture(FakeSapTree.NewApplication());
            var request = new NewSessionModel { Capabilities = new NewSessionModel.CapabilitiesModel() };
            request.Capabilities.AlwaysMatch[SapCapabilities.Options] = JsonDocument.Parse("\"DEV\"").RootElement;

            // Act: create the session.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => fixture.Sessions.NewSession(request));

            // Assert: the failure is a 400 invalid argument and no session was stored.
            Assert.AreEqual(WebDriverErrorCodes.InvalidArgument, exception.Error);
            Assert.IsEmpty(fixture.SessionStore);
        }

        [TestMethod(DisplayName = "Verify that DeleteSession detaches without calling SAP GUI and clears element references.")]
        public void DeleteSessionDetachesTest()
        {
            // Arrange: an attached session with one element reference.
            using var fixture = new RepositoryFixture(FakeSapTree.NewApplication());
            var sessionId = fixture.NewSession();
            fixture.Elements.FindElement(sessionId, new LocationStrategyModel { Using = LocationStrategyModel.SapId, Value = "wnd[0]/usr" });
            var callsBefore = fixture.Engine.Calls.Count;

            // Act: delete the session, then delete it again.
            var first = fixture.Sessions.DeleteSession(sessionId);
            var second = fixture.Sessions.DeleteSession(sessionId);

            // Assert: the first call detaches, the second reports 404, and SAP GUI was never called.
            Assert.AreEqual(200, first);
            Assert.AreEqual(404, second);
            Assert.HasCount(callsBefore, fixture.Engine.Calls);
        }

        [TestMethod(DisplayName = "Verify that element references are stable per SAP identifier and become stale when the control disappears.")]
        public void ElementReferenceLifecycleTest()
        {
            // Arrange: an attached session with a button.
            var application = FakeSapTree.NewApplication();
            var userArea = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]/usr");
            var button = FakeSapTree.AddControl(userArea, name: "btnOK", type: "GuiButton").With("Text", "OK");
            using var fixture = new RepositoryFixture(application);
            var sessionId = fixture.NewSession();
            var locator = new LocationStrategyModel { Using = LocationStrategyModel.Xpath, Value = "//GuiButton" };

            // Act: find the button twice, click it, then remove it and click again.
            var first = fixture.Elements.FindElement(sessionId, locator).ElementModel;
            var second = fixture.Elements.FindElement(sessionId, locator).ElementModel;
            var clickStatus = fixture.Elements.InvokeClick(sessionId, first.Id);
            userArea.Children.Remove(button);
            var staleStatus = fixture.Elements.InvokeClick(sessionId, first.Id);

            // Assert: one reference per control, press() was invoked once, and the removed control is stale.
            Assert.AreEqual(first.Id, second.Id);
            Assert.AreEqual(200, clickStatus);
            var expectedInvocations = new[] { "press()" };
            Assert.AreSequenceEqual(expectedInvocations, button.Invocations);
            Assert.AreEqual(404, staleStatus);
        }

        [TestMethod(DisplayName = "Verify that element operations wait for a busy session and fail with sessionBusy when it stays busy.")]
        public void ElementOperationBusyTest()
        {
            // Arrange: an attached session that is busy.
            var application = FakeSapTree.NewApplication();
            var session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
            using var fixture = new RepositoryFixture(application);
            var sessionId = fixture.NewSession();
            session.Properties["Busy"] = true;

            // Act: read the page source.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => fixture.Documents.GetPageSource(sessionId));

            // Assert: the failure is classified as a busy session.
            Assert.AreEqual(SapErrorKinds.SessionBusy, exception.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that window operations list handles, switch by name, and maximize the selected window.")]
        public void WindowOperationsTest()
        {
            // Arrange: a session with a popup window.
            var application = FakeSapTree.NewApplication();
            var session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
            var popup = FakeSapTree.AddWindow(session, title: "Information", handle: 0x2B3C);
            using var fixture = new RepositoryFixture(application);
            var sessionId = fixture.NewSession();

            // Act: list handles, switch to the popup by name, read the current handle, and maximize.
            var (_, handles) = fixture.Sessions.GetHandles(sessionId);
            var switchStatus = fixture.Sessions.SwitchWindow(sessionId, "wnd[1]");
            var (_, handleJson) = fixture.Sessions.GetHandle(sessionId);
            var (_, rectangle) = fixture.Sessions.SetWindowMaximized(sessionId);
            var missingStatus = fixture.Sessions.SwitchWindow(sessionId, "wnd[7]");

            // Assert: both windows are listed, the popup became current and was maximized, and an unknown window is 404.
            var expectedHandles = new[] { "1A2B", "2B3C" };
            Assert.AreSequenceEqual(expectedHandles, handles);
            Assert.AreEqual(200, switchStatus);
            Assert.AreEqual("{\"handle\":\"2B3C\",\"name\":\"Information\"}", handleJson);
            var expectedInvocations = new[] { "setFocus()", "maximize()" };
            Assert.AreSequenceEqual(expectedInvocations, popup.Invocations);
            Assert.AreEqual(810, rectangle.Right);
            Assert.AreEqual(404, missingStatus);
        }

        [TestMethod(DisplayName = "Verify that NewScreenshot returns the HardCopy PNG and removes the temporary file.")]
        public void NewScreenshotTest()
        {
            // Arrange: make HardCopy write a minimal PNG signature to the requested path.
            var application = FakeSapTree.NewApplication();
            var window = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]");
            var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01 };
            var writtenPath = string.Empty;
            window.Methods["HardCopy"] = arguments =>
            {
                writtenPath = (string)arguments[0];
                File.WriteAllBytes(writtenPath, pngBytes);
                return writtenPath;
            };
            using var fixture = new RepositoryFixture(application);
            var sessionId = fixture.NewSession();

            // Act: capture the screenshot.
            var (statusCode, base64) = fixture.Sessions.NewScreenshot(sessionId);

            // Assert: the PNG is returned, PNG format (2) was requested, and the temporary file is gone.
            Assert.AreEqual(200, statusCode);
            Assert.AreSequenceEqual(pngBytes, Convert.FromBase64String(base64));
            Assert.EndsWith(",2)", window.Invocations.Single());
            Assert.IsFalse(File.Exists(writtenPath));
        }

        [TestMethod(DisplayName = "Verify that SendActions sends converted virtual keys to the current window and rejects unsupported keys up front.")]
        public void SendActionsTest()
        {
            // Arrange: an attached session.
            var application = FakeSapTree.NewApplication();
            var window = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]");
            using var fixture = new RepositoryFixture(application);
            var sessionId = fixture.NewSession();
            var session = fixture.SessionStore[sessionId];
            var enterThenF3 = NewKeyDowns("", "");
            var enterThenText = NewKeyDowns("", "x");

            // Act: send a supported payload, then one with a character key.
            fixture.Actions.SendActions(session, enterThenF3);
            Assert.ThrowsExactly<SapDriverException>(() => fixture.Actions.SendActions(session, enterThenText));

            // Assert: only the supported payload reached SAP GUI.
            var expectedInvocations = new[] { "sendVKey(0)", "sendVKey(3)" };
            Assert.AreSequenceEqual(expectedInvocations, window.Invocations);
        }

        [TestMethod(DisplayName = "Verify that GetStatusBar and GetConnections return COM-free models without user names.")]
        public void StatusBarAndDiscoveryTest()
        {
            // Arrange: an attached session.
            using var fixture = new RepositoryFixture(FakeSapTree.NewApplication());
            var sessionId = fixture.NewSession();

            // Act: read the status bar and discover sessions.
            var (_, statusBar) = fixture.Sessions.GetStatusBar(sessionId);
            var (_, connections) = fixture.Sessions.GetConnections();

            // Assert: the status bar fields are mapped and discovery carries identifiers and metadata only.
            Assert.AreEqual("S", statusBar.MessageType);
            Assert.AreEqual("Saved", statusBar.Text);
            var json = JsonSerializer.Serialize(connections);
            Assert.Contains("/app/con[0]/ses[0]", json);
            Assert.DoesNotContain("SECRET-USER", json);
        }

        // Builds a key source whose items are keyDown/keyUp pairs for each value.
        private static ActionsModel NewKeyDowns(params string[] values)
        {
            var items = values
                .SelectMany(i => new[]
                {
                    new Dictionary<string, object> { ["type"] = "keyDown", ["value"] = i },
                    new Dictionary<string, object> { ["type"] = "keyUp", ["value"] = i }
                })
                .ToList();

            return new ActionsModel { Actions = [new ActionsModel.ActionModel { Actions = items, Id = "keyboard", Type = "key" }] };
        }

        // Builds a new session request whose alwaysMatch carries the given sap:options object.
        private static NewSessionModel NewSessionRequest(Dictionary<string, object> options)
        {
            var request = new NewSessionModel { Capabilities = new NewSessionModel.CapabilitiesModel() };
            request.Capabilities.AlwaysMatch[SapCapabilities.Options] = options;

            return request;
        }

        // Owns one host, engine, and repository set over a fake tree; disposing stops the STA thread.
        private sealed class RepositoryFixture : IDisposable
        {
            // Owns the dispatcher thread used by every repository in the fixture.
            private readonly SapScriptingHost _host;

            public RepositoryFixture(FakeSapGuiNode application)
            {
                // Wire the production repositories to a real host over the fake engine.
                Engine = new FakeSapGuiEngine(application);
                _host = TestHosts.NewHost(Engine);
                var settings = TestHosts.NewSettings();

                Actions = new ActionsRepository(_host, settings);
                Documents = new DocumentRepository(SessionStore, _host, settings);
                Elements = new ElementsRepository(SessionStore, _host, settings);
                Sessions = new SessionsRepository(SessionStore, _host, settings, NullLogger<SessionsRepository>.Instance);
            }

            public ActionsRepository Actions { get; }

            public DocumentRepository Documents { get; }

            public ElementsRepository Elements { get; }

            public FakeSapGuiEngine Engine { get; }

            public SessionsRepository Sessions { get; }

            public ConcurrentDictionary<string, SapSessionModel> SessionStore { get; } = new();

            public void Dispose()
            {
                // Stop the dispatcher thread.
                _host.Dispose();
            }

            public string NewSession()
            {
                // Attach without criteria, which succeeds because the fake tree has one session.
                Sessions.NewSession(new NewSessionModel { Capabilities = new NewSessionModel.CapabilitiesModel() });

                return SessionStore.Keys.Single();
            }
        }
    }
}
