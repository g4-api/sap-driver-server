using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    public partial class SimulatorEndToEndTests
    {
        // Identifies the delayed button of the challenge test lab, relative to the session.
        private const string DelayedButtonId = "wnd[0]/usr/btnCHALLENGE_DELAYED";

        // Bounds how long a test waits for a simulator state that appears after a delay.
        private static readonly TimeSpan StateTimeout = TimeSpan.FromSeconds(5);

        [TestMethod(DisplayName = "Verify that a button shown after a delay is missing at first and found by polling.")]
        public async Task ChallengeDelayedButtonTestAsync()
        {
            // Arrange: open the challenge test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_CHALLENGE");

            // Act: look for the button at once, then poll until it appears and press it.
            var atOnce = await client.FindElementAsync(sessionId, NewIdLocator(DelayedButtonId));
            var stopwatch = Stopwatch.StartNew();
            string delayed = null;
            while (delayed is null && stopwatch.Elapsed < StateTimeout)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.CancellationToken);
                delayed = await client.FindElementAsync(sessionId, NewIdLocator(DelayedButtonId));
            }

            await client.ClickAsync(sessionId, delayed ?? throw new AssertFailedException("The delayed button never appeared."));
            var statusBar = await client.GetStatusBarAsync(sessionId);

            // Assert: the first lookup missed, and the polled button worked.
            Assert.IsNull(atOnce);
            Assert.AreEqual(("S", "110"), (statusBar.MessageType, statusBar.MessageNumber));
        }

        [TestMethod(DisplayName = "Verify that the session reports Busy during a long round trip and the driver waits before sending a key.")]
        public async Task ChallengeBusyTestAsync()
        {
            // Arrange: open the challenge test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            using var host = NewScriptingHost();
            var inspector = new InspectorService(host);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_CHALLENGE");

            // Act: start the long task, read Busy, then send Enter through the driver while the session is busy.
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/btnCHALLENGE_BUSY"));
            var busy = InvokeMember(inspector, "/app/con[0]/ses[0]", MemberInvocation.GetProperty, "Busy");
            var stopwatch = Stopwatch.StartNew();
            await SendEnterAsync(client, sessionId);
            var waited = stopwatch.Elapsed;
            var idle = InvokeMember(inspector, "/app/con[0]/ses[0]", MemberInvocation.GetProperty, "Busy");
            var statusBar = await client.GetStatusBarAsync(sessionId);

            // Assert: the session was busy, the driver held Enter back until it was idle, and Enter then ran the checks.
            Assert.AreEqual("true", busy);
            Assert.IsGreaterThan(TimeSpan.FromMilliseconds(800), waited);
            Assert.AreEqual("false", idle);
            Assert.AreEqual(("S", "116"), (statusBar.MessageType, statusBar.MessageNumber));
        }

        [TestMethod(DisplayName = "Verify that Post asks for confirmation only on some presses, so scripts must check for wnd[1].")]
        public async Task ChallengeOptionalPopupTestAsync()
        {
            // Arrange: open the challenge test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_CHALLENGE");
            var post = await FindAsync(client, sessionId, "wnd[0]/usr/btnCHALLENGE_POST");

            // Act: press Post three times, answering or cancelling the popup when one appears.
            await client.ClickAsync(sessionId, post);
            var firstPopup = await client.FindElementAsync(sessionId, NewIdLocator("wnd[1]"));
            var firstStatus = await client.GetStatusBarAsync(sessionId);
            await client.ClickAsync(sessionId, post);
            var secondPopup = await client.FindElementAsync(sessionId, NewIdLocator("wnd[1]"));
            await client.SwitchToWindowAsync(sessionId, "wnd[1]");
            await SendEnterAsync(client, sessionId);
            await client.SwitchToWindowAsync(sessionId, "wnd[0]");
            var secondStatus = await client.GetStatusBarAsync(sessionId);
            await client.ClickAsync(sessionId, post);
            await client.SwitchToWindowAsync(sessionId, "wnd[1]");
            await client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 12 });
            await client.SwitchToWindowAsync(sessionId, "wnd[0]");
            var thirdStatus = await client.GetStatusBarAsync(sessionId);

            // Assert: no popup, then a popup answered Yes, then a popup cancelled.
            Assert.IsNull(firstPopup);
            Assert.AreEqual("112", firstStatus.MessageNumber);
            Assert.IsNotNull(secondPopup);
            Assert.AreEqual("Popup answered: Yes", secondStatus.Text);
            Assert.AreEqual("022", thirdStatus.MessageNumber);
        }

        [TestMethod(DisplayName = "Verify that duplicate texts make text locators ambiguous and the chosen mode decides a field's identifier.")]
        public async Task ChallengeIdentityTestAsync()
        {
            // Arrange: open the challenge test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_CHALLENGE");
            const string modeAField = "wnd[0]/usr/subCHALLENGE_DETAIL:SAPMZSIM_CHALLENGE:0310/txtCHALLENGE-A_VALUE";
            const string modeBField = "wnd[0]/usr/subCHALLENGE_DETAIL:SAPMZSIM_CHALLENGE:0320/ctxtCHALLENGE-B_VALUE";

            // Act: find the Select buttons by text, then switch the mode and look for both value fields.
            var selects = await client.FindElementsAsync(sessionId, NewXpathLocator("//GuiButton[@text='Select']"));
            var beforeA = await client.FindElementAsync(sessionId, NewIdLocator(modeAField));
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/radCHALLENGE-MODE_B"));
            var afterA = await client.FindElementAsync(sessionId, NewIdLocator(modeAField));
            var afterB = await FindAsync(client, sessionId, modeBField);
            var containerType = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/subCHALLENGE_DETAIL:SAPMZSIM_CHALLENGE:0320"), "type");

            // Assert: two buttons share the text, and the mode replaced the subscreen and its field.
            Assert.HasCount(2, selects);
            Assert.IsNotNull(beforeA);
            Assert.IsNull(afterA);
            Assert.AreEqual("Beta", await client.GetAttributeAsync(sessionId, afterB, "text"));
            Assert.AreEqual("GuiSimpleContainer", containerType);
        }

        [TestMethod(DisplayName = "Verify that an error locks the other fields until fixed and a warning needs a second Enter.")]
        public async Task ChallengeChecksTestAsync()
        {
            // Arrange: open the challenge test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_CHALLENGE");
            var date = await FindAsync(client, sessionId, "wnd[0]/usr/txtCHALLENGE-DATE");
            var quantity = await FindAsync(client, sessionId, "wnd[0]/usr/txtCHALLENGE-QTY");

            // Act: enter an invalid date, fix it, then enter a high quantity and confirm it with a second Enter.
            await client.SetValueAsync(sessionId, date, "2026-13-45");
            await SendEnterAsync(client, sessionId);
            var error = await client.GetStatusBarAsync(sessionId);
            var lockedQuantity = await client.GetAttributeAsync(sessionId, quantity, "changeable");
            await client.SetValueAsync(sessionId, date, "2026-12-24");
            await SendEnterAsync(client, sessionId);
            var fixedDate = await client.GetStatusBarAsync(sessionId);
            await client.SetValueAsync(sessionId, quantity, "500");
            await SendEnterAsync(client, sessionId);
            var warning = await client.GetStatusBarAsync(sessionId);
            await SendEnterAsync(client, sessionId);
            var confirmed = await client.GetStatusBarAsync(sessionId);

            // Assert: the error locked the quantity, the fix unlocked it, and the warning passed on the second Enter.
            Assert.AreEqual(("E", "113"), (error.MessageType, error.MessageNumber));
            Assert.AreEqual("false", lockedQuantity, ignoreCase: true);
            Assert.AreEqual(("S", "116"), (fixedDate.MessageType, fixedDate.MessageNumber));
            Assert.AreEqual(("W", "115"), (warning.MessageType, warning.MessageNumber));
            Assert.AreEqual(("S", "116"), (confirmed.MessageType, confirmed.MessageNumber));
        }

        [TestMethod(DisplayName = "Verify that a button scrolled out of view lies outside its container and still works through scripting.")]
        public async Task ChallengeOffScreenTestAsync()
        {
            // Arrange: open the challenge test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_CHALLENGE");
            const string containerId = "wnd[0]/usr/ssubCHALLENGE_SCROLL:SAPMZSIM_CHALLENGE:0330";

            // Act: compare the hidden button's position with its container, then press it.
            var container = await client.GetRectAsync(sessionId, await FindAsync(client, sessionId, containerId));
            var hidden = await FindAsync(client, sessionId, $"{containerId}/btnCHALLENGE_HIDDEN");
            var button = await client.GetRectAsync(sessionId, hidden);
            await client.ClickAsync(sessionId, hidden);
            var statusBar = await client.GetStatusBarAsync(sessionId);

            // Assert: the button starts below the container's bottom edge, and pressing it worked anyway.
            Assert.IsGreaterThan(container.Y + container.Height, button.Y);
            Assert.AreEqual(("S", "114"), (statusBar.MessageType, statusBar.MessageNumber));
        }
    }
}
