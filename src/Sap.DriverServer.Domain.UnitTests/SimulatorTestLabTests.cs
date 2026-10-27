using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;

namespace Sap.DriverServer.Domain.UnitTests
{
    public partial class SimulatorEndToEndTests
    {
        // Identifies the hit list grid of an input help popup.
        private const string HitListId = "/app/con[0]/ses[0]/wnd[1]/usr/cntlRESULTLIST/shellcont/shell";

        // Identifies the SAP Easy Access menu tree shell.
        private const string MenuTreeId = MainWindowId + "/usr/cntlIMAGE_CONTAINER/shellcont/shell";

        // Identifies the main window's status bar.
        private const string StatusBarId = MainWindowId + "/sbar";

        [TestMethod(DisplayName = "Verify that SAP Easy Access lists the test lab transactions and the command field starts them.")]
        public async Task TestLabCatalogTestAsync()
        {
            // Arrange: log on through the driver.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);

            // Act: read the test lab folder and its first entries, then start both transactions by OK code.
            var folder = await CallAsync(client, sessionId, MenuTreeId, "getNodeTextByKey", "N4");
            var fields = await CallAsync(client, sessionId, MenuTreeId, "getNodeTextByKey", "N5");
            await RunCommandAsync(client, sessionId, "/nZSIM_FIELDS");
            var fieldsTitle = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]"), "text");
            await RunCommandAsync(client, sessionId, "/nzsim_messages");
            var messagesTitle = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]"), "text");

            // Assert: the menu names the transactions, and the codes start them case-insensitively.
            Assert.AreEqual("Test lab", folder);
            Assert.AreEqual("Test Lab: Field Attributes (ZSIM_FIELDS)", fields);
            Assert.AreEqual("Test Lab: Field Attributes", fieldsTitle);
            Assert.AreEqual("Test Lab: Messages and Popups", messagesTitle);
        }

        [TestMethod(DisplayName = "Verify that field attributes report required, numeric, length, padding, and changeable states.")]
        public async Task FieldAttributesTestAsync()
        {
            // Arrange: open the field attributes test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_FIELDS");
            var companyCode = await FindAsync(client, sessionId, "wnd[0]/usr/ctxtZSIM_FIELDS-BUKRS");
            var shortCode = await FindAsync(client, sessionId, "wnd[0]/usr/txtZSIM_FIELDS-SHORT");
            var documentNumber = await FindAsync(client, sessionId, "wnd[0]/usr/txtZSIM_FIELDS-BELNR");

            // Act: read the attributes, set a text longer than the field, and read the padded output both ways.
            var required = await client.GetAttributeAsync(sessionId, companyCode, "required");
            var numerical = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/txtZSIM_FIELDS-AMOUNT"), "numerical");
            var createdBy = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/txtZSIM_FIELDS-ERNAM"), "changeable");
            var period = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/txtZSIM_FIELDS-MONAT"), "changeable");
            await client.SetValueAsync(sessionId, shortCode, "ABCDEFG");
            var shortText = await client.GetAttributeAsync(sessionId, shortCode, "text");
            var documentText = await client.GetAttributeAsync(sessionId, documentNumber, "text");
            var displayedText = await ReadAsync(client, sessionId, $"{MainWindowId}/usr/txtZSIM_FIELDS-BELNR", "DisplayedText");

            // Assert: SAP-style attributes, a cut text, and a padded field whose Text is trimmed.
            Assert.AreEqual("true", required, ignoreCase: true);
            Assert.AreEqual("true", numerical, ignoreCase: true);
            Assert.AreEqual("false", createdBy, ignoreCase: true);
            Assert.AreEqual("false", period, ignoreCase: true);
            Assert.AreEqual("ABCD", shortText);
            Assert.AreEqual("4711", documentText);
            Assert.AreEqual("      4711", displayedText);
        }

        [TestMethod(DisplayName = "Verify that Enter checks the required entry and the numeric format before it reports success.")]
        public async Task FieldChecksTestAsync()
        {
            // Arrange: open the field attributes test lab with an empty required field.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_FIELDS");
            var amount = await FindAsync(client, sessionId, "wnd[0]/usr/txtZSIM_FIELDS-AMOUNT");

            // Act: press Enter three times: without the required entry, with a text amount, and with valid entries.
            await SendEnterAsync(client, sessionId);
            var missing = await client.GetStatusBarAsync(sessionId);
            await client.SetValueAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/ctxtZSIM_FIELDS-BUKRS"), "1000");
            await client.SetValueAsync(sessionId, amount, "twelve");
            await SendEnterAsync(client, sessionId);
            var format = await client.GetStatusBarAsync(sessionId);
            await client.SetValueAsync(sessionId, amount, "2,500.00");
            await SendEnterAsync(client, sessionId);
            var success = await client.GetStatusBarAsync(sessionId);

            // Assert: each check reports its own message number, and the last Enter succeeds.
            Assert.AreEqual(("E", "050"), (missing.MessageType, missing.MessageNumber));
            Assert.AreEqual(("E", "051"), (format.MessageType, format.MessageNumber));
            Assert.AreEqual(("S", "052"), (success.MessageType, success.MessageNumber));
        }

        [TestMethod(DisplayName = "Verify that F4 opens the hit list for the focused field and copying an entry fills the field.")]
        public async Task ValueHelpTestAsync()
        {
            // Arrange: open the field attributes test lab and focus the airline field.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_FIELDS");
            var airline = await FindAsync(client, sessionId, "wnd[0]/usr/ctxtZSIM_FIELDS-CARRID");
            await client.ClickAsync(sessionId, airline);

            // Act: press F4, read the hit list, select the fourth entry, and copy it with Enter.
            await client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 4, Window = "wnd[0]" });
            await client.SwitchToWindowAsync(sessionId, "wnd[1]");
            var title = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[1]"), "text");
            var rowCount = await ReadAsync(client, sessionId, HitListId, "RowCount");
            await SetAsync(client, sessionId, HitListId, "selectedRows", "3");
            await SendEnterAsync(client, sessionId);
            var popup = await client.FindElementAsync(sessionId, NewIdLocator("wnd[1]"));
            await client.SwitchToWindowAsync(sessionId, "wnd[0]");
            var value = await client.GetAttributeAsync(sessionId, airline, "text");

            // Assert: the hit list lists five airlines, and the copied key closed the popup and filled the field.
            Assert.AreEqual("Airline: 5 Entries found", title);
            Assert.AreEqual("5", rowCount);
            Assert.IsNull(popup);
            Assert.AreEqual("SIM", value);
        }

        [TestMethod(DisplayName = "Verify that F4 on a field without input help reports a warning and opens no popup.")]
        public async Task ValueHelpMissingTestAsync()
        {
            // Arrange: open the field attributes test lab and focus a field without input help.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_FIELDS");
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/txtZSIM_FIELDS-SHORT"));

            // Act: press F4.
            await client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 4, Window = "wnd[0]" });
            var statusBar = await client.GetStatusBarAsync(sessionId);
            var popup = await client.FindElementAsync(sessionId, NewIdLocator("wnd[1]"));

            // Assert: the status bar names the missing input help, and no popup opened.
            Assert.AreEqual(("W", "007"), (statusBar.MessageType, statusBar.MessageNumber));
            Assert.IsNull(popup);
        }

        [TestMethod(DisplayName = "Verify that S, W, and E messages reach the status bar and I messages open a popup marked MessageAsPopup.")]
        public async Task MessageTypesTestAsync()
        {
            // Arrange: open the messages test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_MESSAGES");

            // Act: raise a warning, then an information message, and read the popup before continuing.
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/btnMSG_W"));
            var warning = await client.GetStatusBarAsync(sessionId);
            var warningAsPopup = await ReadAsync(client, sessionId, StatusBarId, "MessageAsPopup");
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/btnMSG_I"));
            var information = await client.GetStatusBarAsync(sessionId);
            var informationAsPopup = await ReadAsync(client, sessionId, StatusBarId, "MessageAsPopup");
            await client.SwitchToWindowAsync(sessionId, "wnd[1]");
            var popupTitle = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[1]"), "text");
            var popupText = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[1]/usr/txtMESSTXT1"), "text");
            var cancel = await Assert.ThrowsExactlyAsync<SapDriverException>(() => client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 12 }));
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[1]/tbar[0]/btn[0]"));
            var popup = await client.FindElementAsync(sessionId, NewIdLocator("wnd[1]"));

            // Assert: the warning stays in the status bar; the information message is a popup that only continues.
            Assert.AreEqual(("W", "041"), (warning.MessageType, warning.MessageNumber));
            Assert.AreEqual("false", warningAsPopup);
            Assert.AreEqual(("I", "043"), (information.MessageType, information.MessageNumber));
            Assert.AreEqual("true", informationAsPopup);
            Assert.AreEqual("Information", popupTitle);
            Assert.AreEqual(information.Text, popupText);
            Assert.AreEqual(SapErrorKinds.ComFailure, cancel.ErrorKind);
            Assert.IsNull(popup);
        }

        [TestMethod(DisplayName = "Verify that errors can be shown as popups and a termination message ends the transaction.")]
        public async Task MessagePopupsTestAsync()
        {
            // Arrange: open the messages test lab and switch S, W, and E messages to popups.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_MESSAGES");
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/chkMSG_AS_POPUP"));

            // Act: raise an error and continue, then raise a termination message and continue.
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/btnMSG_E"));
            await client.SwitchToWindowAsync(sessionId, "wnd[1]");
            var errorTitle = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[1]"), "text");
            await SendEnterAsync(client, sessionId);
            await client.SwitchToWindowAsync(sessionId, "wnd[0]");
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/btnMSG_A"));
            await client.SwitchToWindowAsync(sessionId, "wnd[1]");
            var abortTitle = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[1]"), "text");
            await SendEnterAsync(client, sessionId);
            await client.SwitchToWindowAsync(sessionId, "wnd[0]");
            var mainTitle = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]"), "text");
            var statusBar = await client.GetStatusBarAsync(sessionId);

            // Assert: the error used a popup, and the termination returned to SAP Easy Access with its message.
            Assert.AreEqual("Error", errorTitle);
            Assert.AreEqual("Termination", abortTitle);
            Assert.AreEqual("SAP Easy Access", mainTitle);
            Assert.AreEqual(("A", "044"), (statusBar.MessageType, statusBar.MessageNumber));
        }

        // Calls an allow-listed control method through the driver's call route and returns the result as text.
        private static async Task<string> CallAsync(SapDriverClient client, string sessionId, string id, string method, params object[] arguments)
        {
            // Resolve the control, call the method, and format a scalar result the way attributes are formatted.
            var result = await client.InvokeElementMethodAsync(sessionId, await FindAsync(client, sessionId, id), method, arguments);

            return ConvertToText(result);
        }

        // Calls an allow-listed control method that returns a collection and reads its items as text.
        private static async Task<IReadOnlyList<string>> CallListAsync(SapDriverClient client, string sessionId, string id, string method)
        {
            // Resolve the control, call the method, and format each array item.
            var result = await client.InvokeElementMethodAsync(sessionId, await FindAsync(client, sessionId, id), method);

            return result.EnumerateArray().Select(ConvertToText).ToList();
        }

        // Formats a JSON result like an attribute value: strings as they are, Booleans in lowercase, numbers as written.
        private static string ConvertToText(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => string.Empty
            };
        }

        // Calls a scripting member through the inspector's local COM path, for session members the driver does not expose.
        private static string InvokeMember(InspectorService inspector, string id, string kind, string name, params object[] arguments)
        {
            // Describe the call the way the inspector's member invoker does, and return its formatted result.
            var invocation = new MemberInvocation { Arguments = arguments, Kind = kind, Name = name };

            return inspector.InvokeMember(id, invocation);
        }

        // Creates the inspector's COM host, which attaches to the simulator directly, without the driver.
        private static SapScriptingHost NewScriptingHost()
        {
            return new SapScriptingHost(new StaComDispatcher(), new ComSapGuiEngine(), TestHosts.NewSettings(), NullLogger<SapScriptingHost>.Instance);
        }

        // Reads a control attribute through the driver, resolving the control by identifier first.
        private static async Task<string> ReadAsync(SapDriverClient client, string sessionId, string id, string name)
        {
            return await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, id), name);
        }

        // Sets an allow-listed control property through the driver's property route.
        private static async Task SetAsync(SapDriverClient client, string sessionId, string id, string name, object value)
        {
            // Resolve the control, then set the property; refused names fail the test with the driver's error.
            await client.SetElementPropertyAsync(sessionId, await FindAsync(client, sessionId, id), name, value);
        }
    }
}
