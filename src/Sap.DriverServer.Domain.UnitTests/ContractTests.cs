using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("Contract")]
    [TestCategory("UnitTest")]
    public class ContractTests
    {
        // Holds the W3C element reference key, which every element response must use.
        private const string ElementKey = "element-6066-11e4-a52e-4f735466cecf";

        [TestMethod(DisplayName = "Verify that GET /status returns the UIA status shape plus the additive sap reachability member.")]
        public async Task StatusShapeTestAsync()
        {
            // Arrange: a server over a fake SAP GUI with one connection.
            using var factory = NewFactory(FakeSapTree.NewApplication());
            using var client = factory.CreateClient();

            // Act: read the status from both route prefixes.
            var root = await GetJsonAsync(client, "/status");
            var hub = await GetJsonAsync(client, "/wd/hub/status");

            // Assert: the UIA members are present and SAP scripting is reported as available.
            var value = root.GetProperty("value");
            Assert.IsTrue(value.GetProperty("ready").GetBoolean());
            Assert.IsTrue(value.TryGetProperty("os", out _));
            Assert.AreEqual(0, value.GetProperty("sessions").GetArrayLength());
            Assert.IsTrue(value.GetProperty("sap").GetProperty("available").GetBoolean());
            Assert.AreEqual(1, value.GetProperty("sap").GetProperty("connections").GetInt32());
            Assert.IsTrue(hub.GetProperty("value").GetProperty("ready").GetBoolean());
        }

        [TestMethod(DisplayName = "Verify that a full session flow works over HTTP: create, find, click, text, source, and delete.")]
        public async Task SessionFlowTestAsync()
        {
            // Arrange: a server over a fake session with one button.
            var application = FakeSapTree.NewApplication();
            var userArea = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]/usr");
            var button = FakeSapTree.AddControl(userArea, name: "btnOK", type: "GuiButton").With("Text", "Continue");
            using var factory = NewFactory(application);
            using var client = factory.CreateClient();

            // Act: create a session selecting the DEV system.
            var newSessionBody = "{\"capabilities\":{\"alwaysMatch\":{\"sap:options\":{\"systemName\":\"DEV\"}}}}";
            var created = await PostJsonAsync(client, "/wd/hub/session", newSessionBody);

            // Assert: the W3C envelope carries a session id and the resolved sap:session capability.
            Assert.AreEqual(HttpStatusCode.OK, created.StatusCode);
            var sessionValue = created.Json.GetProperty("value");
            var sessionId = sessionValue.GetProperty("sessionId").GetString();
            Assert.AreEqual("/app/con[0]/ses[0]", sessionValue.GetProperty("capabilities").GetProperty("sap:session").GetProperty("id").GetString());

            // Act: find the button by XPath and click it.
            var found = await PostJsonAsync(client, $"/session/{sessionId}/element", "{\"using\":\"xpath\",\"value\":\"//GuiButton[@text='Continue']\"}");
            var elementId = found.Json.GetProperty("value").GetProperty(ElementKey).GetString();
            var clicked = await PostJsonAsync(client, $"/session/{sessionId}/element/{elementId}/click", "{}");
            var text = await GetJsonAsync(client, $"/session/{sessionId}/element/{elementId}/text");
            var source = await GetJsonAsync(client, $"/session/{sessionId}/source");

            // Assert: the element reference uses the W3C key, the click pressed the button, and reads return SAP data.
            Assert.AreEqual(HttpStatusCode.OK, clicked.StatusCode);
            var expectedInvocations = new[] { "press()" };
            Assert.AreSequenceEqual(expectedInvocations, button.Invocations);
            Assert.AreEqual("Continue", text.GetProperty("value").GetString());
            Assert.Contains("<GuiButton", source.GetProperty("value").GetString());

            // Act: delete the session and reuse its id.
            var deleted = await SendAsync(client, HttpMethod.Delete, $"/session/{sessionId}");
            var afterDelete = await SendAsync(client, HttpMethod.Get, $"/session/{sessionId}/source");

            // Assert: the session is gone and reported as invalid.
            Assert.AreEqual(HttpStatusCode.OK, deleted.StatusCode);
            Assert.AreEqual(HttpStatusCode.NotFound, afterDelete.StatusCode);
            Assert.AreEqual(WebDriverErrorCodes.InvalidSessionId, afterDelete.Json.GetProperty("value").GetProperty("error").GetString());
        }

        [TestMethod(DisplayName = "Verify that an ambiguous selection returns session not created with sapErrorKind and the candidates.")]
        public async Task AmbiguousSessionErrorTestAsync()
        {
            // Arrange: two open sessions on one connection.
            var application = FakeSapTree.NewApplication();
            var connection = (FakeSapGuiNode)application.FindById("/app/con[0]");
            FakeSapTree.AddSession(connection, systemName: "DEV", client: "100");
            using var factory = NewFactory(application);
            using var client = factory.CreateClient();

            // Act: create a session without criteria.
            var response = await PostJsonAsync(client, "/session", "{\"capabilities\":{\"alwaysMatch\":{}}}");

            // Assert: the W3C error carries the SAP kind and both candidate identifiers, and no stack trace.
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            var error = response.Json.GetProperty("value");
            Assert.AreEqual(WebDriverErrorCodes.SessionNotCreated, error.GetProperty("error").GetString());
            Assert.AreEqual(SapErrorKinds.AmbiguousSession, error.GetProperty("data").GetProperty("sapErrorKind").GetString());
            Assert.AreEqual(2, error.GetProperty("data").GetProperty("candidates").GetArrayLength());
            Assert.IsFalse(error.TryGetProperty("stackTrace", out _));
        }

        [TestMethod(DisplayName = "Verify that SAP GUI being unavailable returns session not created with sapGuiNotRunning.")]
        public async Task SapGuiNotRunningErrorTestAsync()
        {
            // Arrange: an engine whose attachment fails because SAP Logon is closed.
            var engine = new FakeSapGuiEngine(application: null)
            {
                AttachException = SapDriverException.NewSessionNotCreated(SapErrorKinds.SapGuiNotRunning, "SAP GUI is not running.")
            };
            using var factory = NewFactory(engine);
            using var client = factory.CreateClient();

            // Act: create a session and read the status.
            var response = await PostJsonAsync(client, "/session", "{\"capabilities\":{}}");
            var status = await GetJsonAsync(client, "/status");

            // Assert: session creation fails with the kind, and status reports scripting as unavailable without failing.
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.AreEqual(SapErrorKinds.SapGuiNotRunning, response.Json.GetProperty("value").GetProperty("data").GetProperty("sapErrorKind").GetString());
            Assert.IsFalse(status.GetProperty("value").GetProperty("sap").GetProperty("available").GetBoolean());
        }

        [TestMethod(DisplayName = "Verify that malformed sap:options are rejected as invalid argument before SAP GUI is contacted.")]
        public async Task InvalidOptionsBeforeAttachTestAsync()
        {
            // Arrange: an engine that fails every attachment, so any SAP contact surfaces as sapGuiNotRunning.
            var engine = new FakeSapGuiEngine(application: null)
            {
                AttachException = SapDriverException.NewSessionNotCreated(SapErrorKinds.SapGuiNotRunning, "SAP GUI is not running.")
            };
            using var factory = NewFactory(engine);
            using var client = factory.CreateClient();

            // Act: create a session with a malformed session path.
            var response = await PostJsonAsync(client, "/session", "{\"capabilities\":{\"alwaysMatch\":{\"sap:options\":{\"sessionPath\":\"bogus\"}}}}");

            // Assert: the typo is reported as a 400 invalid argument and the engine was never asked to attach.
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(WebDriverErrorCodes.InvalidArgument, response.Json.GetProperty("value").GetProperty("error").GetString());
            Assert.DoesNotContain("GetApplication", engine.Calls);
        }

        [TestMethod(DisplayName = "Verify that a raw COM failure is classified and its SAP message never reaches the client.")]
        public async Task ComFailureHidesDetailsTestAsync()
        {
            // Arrange: a button whose press raises a COM error quoting screen data.
            var application = FakeSapTree.NewApplication();
            var userArea = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]/usr");
            var button = FakeSapTree.AddControl(userArea, name: "btnOK", type: "GuiButton").With("Text", "OK");
            button.Methods["press"] = _ => throw new COMException("Material 4711 is locked by user JDOE", unchecked((int)0x80004005));
            using var factory = NewFactory(application);
            using var client = factory.CreateClient();
            var sessionId = await NewSessionAsync(client);
            var elementId = await FindElementAsync(client, sessionId, new LocationStrategyModel { Using = "id", Value = "wnd[0]/usr/btnOK" });

            // Act: click the button.
            var response = await PostJsonAsync(client, $"/session/{sessionId}/element/{elementId}/click", "{}");

            // Assert: the client receives a classified error without the COM text.
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.AreEqual(SapErrorKinds.ComFailure, response.Json.GetProperty("value").GetProperty("data").GetProperty("sapErrorKind").GetString());
            Assert.DoesNotContain("4711", response.Body);
            Assert.DoesNotContain("JDOE", response.Body);
        }

        [TestMethod(DisplayName = "Verify that User32, execute script, pointer actions, and shell clicks return structured unsupported operations.")]
        public async Task UnsupportedOperationsTestAsync()
        {
            // Arrange: a session with a grid shell.
            var application = FakeSapTree.NewApplication();
            var userArea = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]/usr");
            var grid = FakeSapTree.AddControl(userArea, name: "cntlGRID/shellcont/shell", type: "GuiShell");
            grid.Properties["SubType"] = "GridView";
            using var factory = NewFactory(application);
            using var client = factory.CreateClient();
            var sessionId = await NewSessionAsync(client);
            var gridId = await FindElementAsync(client, sessionId, new LocationStrategyModel { Using = "tag name", Value = "GuiShell" });
            const string pointerActions = "{\"actions\":[{\"type\":\"pointer\",\"id\":\"m\",\"actions\":[{\"type\":\"pointerDown\",\"button\":0}]}]}";

            // Act: call every unsupported surface.
            var responses = new[]
            {
                await PostJsonAsync(client, $"/session/{sessionId}/user32/click", "{}"),
                await SendAsync(client, HttpMethod.Get, $"/session/{sessionId}/user32/layouts"),
                await PostJsonAsync(client, $"/session/{sessionId}/execute/sync", "{\"script\":\"Get-Process\"}"),
                await PostJsonAsync(client, $"/session/{sessionId}/actions", pointerActions),
                await PostJsonAsync(client, $"/session/{sessionId}/element/{gridId}/click", "{}")
            };

            // Assert: each is a 500 "unsupported operation" tagged notExposedByScripting.
            foreach (var response in responses)
            {
                Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode, response.Body);
                var error = response.Json.GetProperty("value");
                Assert.AreEqual(WebDriverErrorCodes.UnsupportedOperation, error.GetProperty("error").GetString());
                Assert.AreEqual(SapErrorKinds.NotExposedByScripting, error.GetProperty("data").GetProperty("sapErrorKind").GetString());
            }

            Assert.IsEmpty(grid.Invocations);
        }

        [TestMethod(DisplayName = "Verify that invalid requests keep the UIA bad request, no such element, and invalid selector shapes.")]
        public async Task InvalidRequestShapesTestAsync()
        {
            // Arrange: a session over the default fake tree.
            using var factory = NewFactory(FakeSapTree.NewApplication());
            using var client = factory.CreateClient();
            var sessionId = await NewSessionAsync(client);

            // Act: send a body without capabilities, an unmatched locator, and a malformed XPath.
            var badRequest = await PostJsonAsync(client, "/session", "{}");
            var notFound = await PostJsonAsync(client, $"/session/{sessionId}/element", "{\"using\":\"xpath\",\"value\":\"//GuiTree\"}");
            var badSelector = await PostJsonAsync(client, $"/session/{sessionId}/element", "{\"using\":\"xpath\",\"value\":\"//[\"}");

            // Assert: each failure uses its documented status and error code.
            Assert.AreEqual(HttpStatusCode.BadRequest, badRequest.StatusCode);
            Assert.AreEqual("Bad Request", badRequest.Json.GetProperty("value").GetProperty("error").GetString());
            Assert.AreEqual(HttpStatusCode.NotFound, notFound.StatusCode);
            Assert.AreEqual(WebDriverErrorCodes.NoSuchElement, notFound.Json.GetProperty("value").GetProperty("error").GetString());
            Assert.AreEqual(HttpStatusCode.BadRequest, badSelector.StatusCode);
            Assert.AreEqual(WebDriverErrorCodes.InvalidSelector, badSelector.Json.GetProperty("value").GetProperty("error").GetString());
        }

        [TestMethod(DisplayName = "Verify that the SAP endpoints discover sessions, send virtual keys, and read the status bar.")]
        public async Task SapEndpointsTestAsync()
        {
            // Arrange: a session over the default fake tree.
            var application = FakeSapTree.NewApplication();
            var window = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]");
            using var factory = NewFactory(application);
            using var client = factory.CreateClient();
            var sessionId = await NewSessionAsync(client);

            // Act: discover, send F3 (Back), and read the status bar.
            var sessions = await GetJsonAsync(client, "/sap/sessions");
            var vkey = await PostJsonAsync(client, $"/session/{sessionId}/sap/vkey", "{\"virtualKey\":3}");
            var statusBar = await GetJsonAsync(client, $"/session/{sessionId}/sap/statusbar");

            // Assert: discovery lists the session, the key reached the window, and the status bar is mapped.
            var connection = sessions.GetProperty("value").EnumerateArray().Single();
            Assert.AreEqual("/app/con[0]/ses[0]", connection.GetProperty("sessions")[0].GetProperty("id").GetString());
            Assert.AreEqual(HttpStatusCode.OK, vkey.StatusCode);
            var expectedInvocations = new[] { "sendVKey(3)" };
            Assert.AreSequenceEqual(expectedInvocations, window.Invocations);
            Assert.AreEqual("S", statusBar.GetProperty("value").GetProperty("messageType").GetString());
        }

        [TestMethod(DisplayName = "Verify that the rect and element-at endpoints return W3C geometry and a reusable element reference.")]
        public async Task ElementGeometryEndpointsTestAsync()
        {
            // Arrange: a button with geometry that findByPosition reports for one point only.
            var application = FakeSapTree.NewApplication();
            var userArea = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]/wnd[0]/usr");
            var session = (FakeSapGuiNode)application.FindById("/app/con[0]/ses[0]");
            FakeSapTree.AddControl(userArea, name: "btnOK", type: "GuiButton")
                .With("ScreenLeft", 100)
                .With("ScreenTop", 200)
                .With("Width", 50)
                .With("Height", 20);
            session.Methods["findByPosition"] = arguments => (int)arguments[0] == 110
                ? new[] { "/app/con[0]/ses[0]/wnd[0]/usr/btnOK", "Cell(1,A)" }
                : Array.Empty<string>();
            using var factory = NewFactory(application);
            using var client = factory.CreateClient();
            var sessionId = await NewSessionAsync(client);

            // Act: hit-test a point on the button and one outside it, then read the rect through the issued reference.
            var hit = await PostJsonAsync(client, $"/session/{sessionId}/sap/element/at", "{\"x\":110,\"y\":205}");
            var miss = await PostJsonAsync(client, $"/session/{sessionId}/sap/element/at", "{\"x\":1,\"y\":1}");
            var hitValue = hit.Json.GetProperty("value");
            var elementId = hitValue.GetProperty(ElementKey).GetString();
            var rect = await GetJsonAsync(client, $"/session/{sessionId}/element/{elementId}/rect");

            // Assert: the hit carries identity, inner object, and geometry; the miss is "no such element".
            Assert.AreEqual(HttpStatusCode.OK, hit.StatusCode);
            Assert.AreEqual("/app/con[0]/ses[0]/wnd[0]/usr/btnOK", hitValue.GetProperty("sapId").GetString());
            Assert.AreEqual("GuiButton", hitValue.GetProperty("type").GetString());
            Assert.AreEqual("Cell(1,A)", hitValue.GetProperty("innerObject").GetString());
            Assert.AreEqual(50, hitValue.GetProperty("rect").GetProperty("width").GetInt32());
            Assert.AreEqual(HttpStatusCode.NotFound, miss.StatusCode);
            Assert.AreEqual(WebDriverErrorCodes.NoSuchElement, miss.Json.GetProperty("value").GetProperty("error").GetString());
            var rectValue = rect.GetProperty("value");
            Assert.AreEqual(100, rectValue.GetProperty("x").GetInt32());
            Assert.AreEqual(200, rectValue.GetProperty("y").GetInt32());
            Assert.AreEqual(50, rectValue.GetProperty("width").GetInt32());
            Assert.AreEqual(20, rectValue.GetProperty("height").GetInt32());
        }

        // Finds one element through HTTP and returns its reference identifier.
        private static async Task<string> FindElementAsync(HttpClient client, string sessionId, LocationStrategyModel locator)
        {
            var response = await PostJsonAsync(client, $"/session/{sessionId}/element", JsonSerializer.Serialize(locator));

            return response.Json.GetProperty("value").GetProperty(ElementKey).GetString();
        }

        // Sends a GET request and parses the JSON body.
        private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
        {
            var response = await SendAsync(client, HttpMethod.Get, path);

            return response.Json;
        }

        // Creates a server whose scripting host runs over a fake engine for the given tree.
        private static WebApplicationFactory<Program> NewFactory(FakeSapGuiNode application)
        {
            return NewFactory(new FakeSapGuiEngine(application));
        }

        // Creates a server whose scripting host runs over the given fake engine and a real STA dispatcher.
        private static WebApplicationFactory<Program> NewFactory(FakeSapGuiEngine engine)
        {
            return new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISapScriptingHost>();
                services.AddSingleton<ISapScriptingHost>(_ => TestHosts.NewHost(engine));
                services.AddSingleton(TestHosts.NewSettings());
            }));
        }

        // Creates a session without criteria and returns its identifier.
        private static async Task<string> NewSessionAsync(HttpClient client)
        {
            var response = await PostJsonAsync(client, "/session", "{\"capabilities\":{}}");

            return response.Json.GetProperty("value").GetProperty("sessionId").GetString();
        }

        // Sends a POST request with a JSON body and returns the status, raw body, and parsed JSON.
        private static async Task<(HttpStatusCode StatusCode, string Body, JsonElement Json)> PostJsonAsync(HttpClient client, string path, string body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            return await ReadResponseAsync(client, request);
        }

        // Sends a request and returns the status, raw body, and parsed JSON; an empty body parses to default.
        private static async Task<(HttpStatusCode StatusCode, string Body, JsonElement Json)> ReadResponseAsync(HttpClient client, HttpRequestMessage request)
        {
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            var json = string.IsNullOrWhiteSpace(text) ? default : JsonDocument.Parse(text).RootElement.Clone();

            return (response.StatusCode, text, json);
        }

        // Sends a request without a body and returns the status, raw body, and parsed JSON.
        private static async Task<(HttpStatusCode StatusCode, string Body, JsonElement Json)> SendAsync(HttpClient client, HttpMethod method, string path)
        {
            using var request = new HttpRequestMessage(method, path);

            return await ReadResponseAsync(client, request);
        }
    }
}
