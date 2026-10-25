using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;
using Sap.DriverServer.Settings;
using Sap.DriverServer.Settings.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("SapSimulator")]
    public partial class SimulatorEndToEndTests
    {
        // Identifies the first session's main window, the target of most steps.
        private const string MainWindowId = "/app/con[0]/ses[0]/wnd[0]";

        // Holds the logon password typed into the simulator; the simulator accepts any value and never keeps it.
        private const string Password = "simulator-only";

        // Names the simulator's system, which every simulated session reports.
        private const string SystemName = "SIM";

        // Bounds how long the published driver may take to answer its first status request.
        private static readonly TimeSpan DriverStartTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the MSTest context, whose cancellation token aborts waits when the run is cancelled.
        /// </summary>
        public TestContext TestContext { get; set; }

        [TestMethod(DisplayName = "Verify that the driver status reports the simulator's connection through the running object table.")]
        public async Task StatusTestAsync()
        {
            // Arrange: start the 32-bit simulator and a driver over the production COM engine.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);

            // Act: read the status, which attaches through the SAPGUI entry.
            var status = await client.GetStatusAsync();

            // Assert: the attach crossed the process and bitness boundary and found the single connection.
            Assert.IsTrue(status.Sap.Available);
            Assert.AreEqual(1, status.Sap.Connections);
        }

        [TestMethod(DisplayName = "Verify that logging on through the driver reaches SAP Easy Access and never reads the password back.")]
        public async Task LogonTestAsync()
        {
            // Arrange: attach a driver session to the simulator's logon screen.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewSessionAsync(client);

            // Act: fill in the logon fields, read the password field back, and press Enter.
            var password = await FindAsync(client, sessionId, "wnd[0]/usr/pwdRSYST-BCODE");
            await client.SetValueAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/txtRSYST-BNAME"), "tester");
            await client.SetValueAsync(sessionId, password, Password);
            var passwordText = await client.GetAttributeAsync(sessionId, password, "text");
            await SendEnterAsync(client, sessionId);
            var statusBar = await client.GetStatusBarAsync(sessionId);
            var source = await client.GetSourceAsync(sessionId);

            // Assert: the password reads as empty, the logon succeeded, and the main window shows SAP Easy Access.
            Assert.AreEqual(string.Empty, passwordText);
            Assert.AreEqual("S", statusBar.MessageType);
            Assert.AreEqual("ZSIM", statusBar.MessageId);
            Assert.AreEqual("000", statusBar.MessageNumber);
            Assert.Contains($"id=\"{MainWindowId}\" type=\"GuiMainWindow\" name=\"wnd\" text=\"SAP Easy Access\"", source);
        }

        [TestMethod(DisplayName = "Verify that OK codes in the command field start transactions and report unknown ones.")]
        public async Task CommandNavigationTestAsync()
        {
            // Arrange: log on.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);

            // Act: start the form transaction, then an unknown one.
            await RunCommandAsync(client, sessionId, "/nFORM");
            var popupButton = await client.FindElementAsync(sessionId, NewIdLocator("wnd[0]/usr/btnOPEN_POPUP"));
            await RunCommandAsync(client, sessionId, "/nNOPE");
            var statusBar = await client.GetStatusBarAsync(sessionId);

            // Assert: the form opened, and the unknown transaction was reported as an error.
            Assert.IsNotNull(popupButton);
            Assert.AreEqual("E", statusBar.MessageType);
            Assert.AreEqual("004", statusBar.MessageNumber);
        }

        [TestMethod(DisplayName = "Verify that clicks, keys, and clears change check boxes, radio buttons, combo boxes, tabs, and text fields.")]
        public async Task FormInteractionTestAsync()
        {
            // Arrange: open the form screen.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nFORM");

            // Act: toggle the check box, pick a radio button and a currency, switch tabs, and clear the price.
            var checkBox = await FindAsync(client, sessionId, "wnd[0]/usr/chkSIM-ACTIVE");
            var business = await FindAsync(client, sessionId, "wnd[0]/usr/radSIM-CLASS_C");
            var currency = await FindAsync(client, sessionId, "wnd[0]/usr/cmbSIM-CURRENCY");
            var price = await FindAsync(client, sessionId, "wnd[0]/usr/txtSIM-PRICE");
            await client.ClickAsync(sessionId, checkBox);
            await client.ClickAsync(sessionId, business);
            await client.SetValueAsync(sessionId, currency, "USD");
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/tabsTABSTRIP/tabpNOTES"));
            await client.ClearAsync(sessionId, price);

            // Assert: every control reports its new state, and only the selected tab's fields exist.
            Assert.AreEqual("false", await client.GetAttributeAsync(sessionId, checkBox, "selected"));
            Assert.AreEqual("true", await client.GetAttributeAsync(sessionId, business, "selected"));
            Assert.AreEqual("USD", await client.GetAttributeAsync(sessionId, currency, "key"));
            Assert.AreEqual(string.Empty, await client.GetAttributeAsync(sessionId, price, "text"));
            Assert.IsNotNull(await client.FindElementAsync(sessionId, NewIdLocator("wnd[0]/usr/tabsTABSTRIP/tabpNOTES/ssubNOTES:SAPMZSIM_FORM:0120/txtSIM-NOTE")));
            Assert.IsNull(await client.FindElementAsync(sessionId, NewIdLocator("wnd[0]/usr/tabsTABSTRIP/tabpDETAILS/ssubDETAILS:SAPMZSIM_FORM:0110/txtSIM-PLANETYPE")));
        }

        [TestMethod(DisplayName = "Verify that typing into an output-only field fails with a classified COM failure.")]
        public async Task ReadOnlyFieldTestAsync()
        {
            // Arrange: open the form screen and find its output-only status field.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nFORM");
            var status = await FindAsync(client, sessionId, "wnd[0]/usr/txtSIM-STATUS");

            // Act: type into the field.
            var exception = await Assert.ThrowsExactlyAsync<SapDriverException>(() => client.SetValueAsync(sessionId, status, "Changed"));

            // Assert: the refusal arrives as a COM failure without SAP-side details.
            Assert.AreEqual(SapErrorKinds.ComFailure, exception.ErrorKind);
            Assert.AreEqual("false", await client.GetAttributeAsync(sessionId, status, "changeable"));
        }

        [TestMethod(DisplayName = "Verify that the popup becomes wnd[1], blocks wnd[0], captures at its reported size, and closes on Enter.")]
        public async Task PopupWindowTestAsync()
        {
            // Arrange: open the form screen.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nFORM");

            // Act: open the popup, try the blocked main window, then capture and answer the popup.
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/btnOPEN_POPUP"));
            var blocked = await Assert.ThrowsExactlyAsync<SapDriverException>(() => client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 0, Window = "wnd[0]" }));
            await client.SwitchToWindowAsync(sessionId, "wnd[1]");
            var rect = await client.GetRectAsync(sessionId, await FindAsync(client, sessionId, "wnd[1]"));
            var image = await client.GetScreenshotAsync(sessionId);
            await SendEnterAsync(client, sessionId);
            var statusBar = await client.GetStatusBarAsync(sessionId);
            var popup = await client.FindElementAsync(sessionId, NewIdLocator("wnd[1]"));

            // Assert: the main window refused input, the image matches the popup's geometry, and Enter answered Yes.
            Assert.AreEqual(SapErrorKinds.ComFailure, blocked.ErrorKind);
            Assert.AreEqual((rect.Width, rect.Height), GetImageSize(image));
            Assert.AreEqual("Popup answered: Yes", statusBar.Text);
            Assert.IsNull(popup);
        }

        [TestMethod(DisplayName = "Verify that element-at resolves a button at its center and a grid cell as an inner object.")]
        public async Task ElementAtTestAsync()
        {
            // Arrange: open the form screen and read the popup button's rectangle.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nFORM");
            var button = await client.GetRectAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/btnOPEN_POPUP"));

            // Act: hit-test the button's center, then a point in the grid's first column.
            var buttonHit = await client.FindElementAtAsync(sessionId, button.X + (button.Width / 2), button.Y + (button.Height / 2));
            await RunCommandAsync(client, sessionId, "/nGRID");
            var grid = await client.GetRectAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/cntlGRID1/shellcont/shell"));
            var cellHit = await client.FindElementAtAsync(sessionId, grid.X + (grid.Width / 20), grid.Y + (grid.Height / 4));

            // Assert: the button resolves without an inner object; the grid point resolves to a cell of column CARRID.
            Assert.AreEqual($"{MainWindowId}/usr/btnOPEN_POPUP", buttonHit.SapId);
            Assert.AreEqual(string.Empty, buttonHit.InnerObject);
            Assert.AreEqual("GuiShell", cellHit.Type);
            Assert.MatchesRegex(CarrierCellPattern(), cellHit.InnerObject);
        }

        [TestMethod(DisplayName = "Verify that the page source and XPath locators see the simulator's object tree.")]
        public async Task PageSourceAndXpathTestAsync()
        {
            // Arrange: attach to the logon screen.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewSessionAsync(client);

            // Act: read the source and evaluate a type, an attribute, and an absolute-identifier XPath.
            var source = await client.GetSourceAsync(sessionId);
            var textFields = await client.FindElementsAsync(sessionId, NewXpathLocator("//GuiTextField"));
            var password = await client.FindElementAsync(sessionId, NewXpathLocator("//GuiPasswordField[@name='RSYST-BCODE']"));
            var enter = await client.FindElementAsync(sessionId, NewXpathLocator($"//GuiButton[@id='{MainWindowId}/tbar[0]/btn[0]']"));

            // Assert: the source is rooted at the session and every locator matches.
            Assert.StartsWith("<GuiSession", source);
            Assert.HasCount(3, textFields);
            Assert.IsNotNull(password);
            Assert.IsNotNull(enter);
        }

        [TestMethod(DisplayName = "Verify that a refused scripting engine is reported as scriptingDisabled by status and new session.")]
        public async Task ScriptingDisabledTestAsync()
        {
            // Arrange: start the simulator with client-side scripting disabled.
            using var simulator = SimulatorProcess.Start("--scripting-disabled");
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);

            // Act: read the status and request a session.
            var status = await client.GetStatusAsync();
            var exception = await Assert.ThrowsExactlyAsync<SapDriverException>(() => NewSessionAsync(client));

            // Assert: both report the classified reason.
            Assert.IsFalse(status.Sap.Available);
            Assert.AreEqual(SapErrorKinds.ScriptingDisabled, status.Sap.ErrorKind);
            Assert.AreEqual(SapErrorKinds.ScriptingDisabled, exception.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that a connection with scripting disabled by the server is reported as scriptingDisabledByServer.")]
        public async Task DisabledByServerTestAsync()
        {
            // Arrange: start the simulator with server-side scripting disabled.
            using var simulator = SimulatorProcess.Start("--disabled-by-server");
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);

            // Act: request a session on the simulated system.
            var exception = await Assert.ThrowsExactlyAsync<SapDriverException>(() => NewSessionAsync(client));

            // Assert: the selection names the server-side reason.
            Assert.AreEqual(SapErrorKinds.ScriptingDisabledByServer, exception.ErrorKind);
        }

        [TestMethod(DisplayName = "Verify that two sessions make a system-only selection ambiguous and an index selection succeed.")]
        public async Task SessionSelectionTestAsync()
        {
            // Arrange: start the simulator with two sessions.
            using var simulator = SimulatorProcess.Start("--sessions", "2");
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var byIndex = new JsonObject
            {
                [SapCapabilities.Options] = new JsonObject { ["systemName"] = SystemName, ["sessionIndex"] = 1 }
            };

            // Act: select by system only, then by system and session index.
            var ambiguous = await Assert.ThrowsExactlyAsync<SapDriverException>(() => NewSessionAsync(client));
            var sessionId = await client.NewSessionAsync(byIndex);
            var window = await client.GetRectAsync(sessionId, await FindAsync(client, sessionId, "/app/con[0]/ses[1]/wnd[0]"));

            // Assert: the first request names the ambiguity; the second attached to the second session's window.
            Assert.AreEqual(SapErrorKinds.AmbiguousSession, ambiguous.ErrorKind);
            Assert.IsGreaterThan(0, window.Width);
        }

        [TestMethod(DisplayName = "Verify that the inspector's local source reads the tree, generates unique locators, and hit-tests by window handle.")]
        public async Task InspectorLocalSourceTestAsync()
        {
            // Arrange: inspect the simulator through the inspector's own COM host, as the desktop overlay does.
            using var simulator = SimulatorProcess.Start();
            using var host = new SapScriptingHost(new StaComDispatcher(), new ComSapGuiEngine(), TestHosts.NewSettings(), NullLogger<SapScriptingHost>.Instance);
            using var source = new LocalInspectorSource(new InspectorService(host));
            const string userId = MainWindowId + "/usr/txtRSYST-BNAME";

            // Act: read the root, the user field's locators and geometry, and hit-test the field's center.
            var root = await source.GetRootAsync();
            var locators = await source.GenerateLocatorsAsync(userId);
            var rectangle = (await source.GetSnapshotAsync(userId)).Rectangle;
            var center = new InspectorPoint((rectangle.Left + rectangle.Right) / 2, (rectangle.Top + rectangle.Bottom) / 2, simulator.MainWindowHandle);
            var hit = await source.HitTestAsync(center);

            // Assert: the tree starts at the application, a unique locator exists, and the hover lands on the field.
            Assert.AreEqual("/app", root.Id);
            Assert.Contains(i => i.IsUnique, locators);
            Assert.AreEqual(userId, hit?.Id);
        }

        [TestMethod(DisplayName = "Verify that the inspector's driver source captures the screen canvas and hit-tests canvas points.")]
        public async Task InspectorDriverSourceTestAsync()
        {
            // Arrange: create a driver session and inspect it through a driver source that owns it.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewSessionAsync(client);
            var userRect = await client.GetRectAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/txtRSYST-BNAME"));
            using var source = new DriverInspectorSource(TestDrivers.NewClient(factory), sessionId, isOwner: true);

            // Act: capture the canvas, then hit-test the user field's center as a canvas click does.
            var capture = await source.GetScreenCaptureAsync();
            var hit = await source.HitTestAsync(new InspectorPoint(userRect.X + (userRect.Width / 2), userRect.Y + (userRect.Height / 2), WindowHandle: 0));

            // Assert: the image matches the captured window's rectangle, and the point resolves to the field.
            Assert.AreEqual((capture.WindowRect.Width, capture.WindowRect.Height), GetImageSize(capture.Image));
            Assert.AreEqual($"{MainWindowId}/usr/txtRSYST-BNAME", hit?.Id);
        }

        [TestMethod(DisplayName = "Verify that the published single-file driver attaches to the simulator and logs on.")]
        public async Task PublishedDriverTestAsync()
        {
            // Arrange: start the simulator and the published driver executable on a free port.
            using var simulator = SimulatorProcess.Start();
            var driverPath = GetPublishedDriverPath();
            if (!File.Exists(driverPath))
            {
                Assert.Inconclusive($"The published driver is not at '{driverPath}'. Run scripts\\publish.ps1 or set {EnvironmentSettings.SapDriverPathKey}.");
            }

            var port = GetFreePort();
            using var driver = Process.Start(new ProcessStartInfo(driverPath, $"--port {port}") { CreateNoWindow = true, UseShellExecute = false });

            try
            {
                using var client = new SapDriverClient($"http://localhost:{port}/wd/hub");
                await WaitForDriverAsync(client, TestContext.CancellationToken);

                // Act: attach and log on through the published executable.
                var sessionId = await NewLoggedOnSessionAsync(client);
                var statusBar = await client.GetStatusBarAsync(sessionId);
                await client.DeleteSessionAsync(sessionId);

                // Assert: the single-file build reached the simulator and completed the logon.
                Assert.AreEqual("Logon successful (simulated).", statusBar.Text);
            }
            finally
            {
                // Stop the driver in every outcome; it has no shutdown endpoint.
                driver.Kill();
                await driver.WaitForExitAsync(TestContext.CancellationToken);
            }
        }

        // Matches the inner-object description of a cell in the grid's first column, such as Cell(4,CARRID).
        [GeneratedRegex(@"^Cell\(\d+,CARRID\)$")]
        private static partial Regex CarrierCellPattern();

        // Finds a control by SAP identifier, failing the test with the identifier when it is missing.
        private static async Task<string> FindAsync(SapDriverClient client, string sessionId, string id)
        {
            // Resolve through the driver; a miss fails the test at the step that needed the control.
            return await client.FindElementAsync(sessionId, NewIdLocator(id))
                ?? throw new AssertFailedException($"The control '{id}' was not found.");
        }

        // Reserves a free loopback port for the published driver; the port is released before the driver binds it.
        private static int GetFreePort()
        {
            // Let Windows pick a free port, then release it for the driver.
            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        // Reads the width and height from a PNG header (big-endian, at bytes 16 and 20).
        private static (int Width, int Height) GetImageSize(byte[] image)
        {
            return (BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16)), BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20)));
        }

        // Resolves the published driver: the override, or the repository's artifacts folder.
        private static string GetPublishedDriverPath()
        {
            // Prefer an explicit path; otherwise map src\<tests>\bin\<configuration>\<framework> to <repository>\artifacts.
            var configuredPath = AppSettings.Environment.SapDriverPath;
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            var repositoryDirectory = outputDirectory.Parent.Parent.Parent.Parent.Parent.FullName;

            return Path.Combine(repositoryDirectory, "artifacts", "sap-driver-server", "Sap.DriverServer.exe");
        }

        // Creates a SAP identifier locator.
        private static LocationStrategyModel NewIdLocator(string id)
        {
            return new LocationStrategyModel { Using = LocationStrategyModel.SapId, Value = id };
        }

        // Attaches a driver session to the simulator and logs on.
        private static async Task<string> NewLoggedOnSessionAsync(SapDriverClient client)
        {
            // Attach, fill in user and password, and confirm with Enter.
            var sessionId = await NewSessionAsync(client);
            await client.SetValueAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/txtRSYST-BNAME"), "tester");
            await client.SetValueAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/pwdRSYST-BCODE"), Password);
            await SendEnterAsync(client, sessionId);

            return sessionId;
        }

        // Attaches a driver session to the simulated system.
        private static Task<string> NewSessionAsync(SapDriverClient client)
        {
            // Select by system only; the simulator opens one session unless a test asks for more.
            var capabilities = new JsonObject
            {
                [SapCapabilities.Options] = new JsonObject { ["systemName"] = SystemName }
            };

            return client.NewSessionAsync(capabilities);
        }

        // Creates an XPath locator.
        private static LocationStrategyModel NewXpathLocator(string xpath)
        {
            return new LocationStrategyModel { Using = LocationStrategyModel.Xpath, Value = xpath };
        }

        // Types an OK code into the command field and confirms it with Enter.
        private static async Task RunCommandAsync(SapDriverClient client, string sessionId, string command)
        {
            // Type the code into the command field and confirm it.
            await client.SetValueAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/tbar[0]/okcd"), command);
            await SendEnterAsync(client, sessionId);
        }

        // Sends Enter (virtual key 0) to the session's current window.
        private static Task SendEnterAsync(SapDriverClient client, string sessionId)
        {
            return client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 0 });
        }

        // Waits until the published driver answers its status endpoint, or the test run is cancelled.
        private static async Task WaitForDriverAsync(SapDriverClient client, CancellationToken cancellationToken)
        {
            // Poll the status; an unreachable driver is reported as a SapDriverException until it listens.
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                try
                {
                    await client.GetStatusAsync();
                    return;
                }
                catch (SapDriverException) when (stopwatch.Elapsed < DriverStartTimeout)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
                }
            }
        }
    }
}
