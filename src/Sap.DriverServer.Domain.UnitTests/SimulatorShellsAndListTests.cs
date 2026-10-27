using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        // Lists the toolbar button positions whose types the toolbar test reads: one of each type.
        private static readonly int[] ButtonTypePositions = [0, 2, 3, 4];

        // Identifies the picture in the splitter's second pane.
        private const string PictureId = SplitterId + "/shellcont[1]/shell";

        // Identifies the scroll container of the shell controls test lab, relative to the session.
        private const string ScrollAreaId = "wnd[0]/usr/ssubSCROLL_AREA:SAPMZSIM_SHELLS:0200";

        // Identifies the splitter shell of the shell controls test lab.
        private const string SplitterId = MainWindowId + "/usr/cntlSPLITTER/shellcont/shell";

        // Identifies the text edit in the splitter's first pane.
        private const string TextEditId = SplitterId + "/shellcont[0]/shell";

        // Identifies the toolbar control of the shell controls test lab.
        private const string ToolbarControlId = MainWindowId + "/usr/cntlTOOLBAR/shellcont/shell";

        [TestMethod(DisplayName = "Verify that toolbar control buttons report their types and states and refuse disabled or mismatched presses.")]
        public async Task ToolbarControlTestAsync()
        {
            // Arrange: open the shell controls test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_SHELLS");

            // Act: read the buttons, press a disabled and a menu-only button, toggle the check button, and use a menu.
            var buttonCount = await ReadAsync(client, sessionId, ToolbarControlId, "ButtonCount");
            var typeNames = new List<string>();
            foreach (var position in ButtonTypePositions)
            {
                typeNames.Add(await CallAsync(client, sessionId, ToolbarControlId, "getButtonType", position));
            }

            var types = string.Join(',', typeNames);
            var deleteEnabled = await CallAsync(client, sessionId, ToolbarControlId, "getButtonEnabled", 1);
            var disabledPress = await Assert.ThrowsAsync<SapDriverException>(() => CallAsync(client, sessionId, ToolbarControlId, "pressButton", "DELETE"));
            var menuPress = await Assert.ThrowsAsync<SapDriverException>(() => CallAsync(client, sessionId, ToolbarControlId, "pressButton", "MORE"));
            await CallAsync(client, sessionId, ToolbarControlId, "pressButton", "EDIT_MODE");
            var editMode = await CallAsync(client, sessionId, ToolbarControlId, "getButtonChecked", 2);
            await CallAsync(client, sessionId, ToolbarControlId, "pressContextButton", "EXPORT");
            await CallAsync(client, sessionId, ToolbarControlId, "selectContextMenuItem", "EXPORT_PDF");
            var statusBar = await client.GetStatusBarAsync(sessionId);

            // Assert: SAP's button types, a refused disabled and menu-only press, a toggled state, and the menu's function.
            Assert.AreEqual("5", buttonCount);
            Assert.AreEqual("Button,CheckBox,ButtonAndMenu,Menu", types);
            Assert.AreEqual("false", deleteEnabled);
            Assert.IsNotNull(disabledPress);
            Assert.IsNotNull(menuPress);
            Assert.AreEqual("true", editMode);
            Assert.AreEqual("Toolbar function EXPORT_PDF (simulated).", statusBar.Text);
        }

        [TestMethod(DisplayName = "Verify that a splitter holds a text edit and a picture whose members work through their shell identifiers.")]
        public async Task SplitterShellsTestAsync()
        {
            // Arrange: open the shell controls test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_SHELLS");
            var textEdit = await FindAsync(client, sessionId, TextEditId);

            // Act: read the shell subtypes, type through the driver, then use the text edit's members and the picture.
            var splitterType = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, SplitterId), "subType");
            var textEditType = await client.GetAttributeAsync(sessionId, textEdit, "subType");
            await client.SetValueAsync(sessionId, textEdit, "Line A\nLine B");
            var typed = await client.GetAttributeAsync(sessionId, textEdit, "text");
            var lineCount = await ReadAsync(client, sessionId, TextEditId, "LineCount");
            var secondLine = await CallAsync(client, sessionId, TextEditId, "getLineText", 1);
            await CallAsync(client, sessionId, PictureId, "clickPictureArea", 10, 20);
            var statusBar = await client.GetStatusBarAsync(sessionId);

            // Assert: the nested shells are reachable, the driver types into the text edit, and the members work.
            Assert.AreEqual("Splitter", splitterType);
            Assert.AreEqual("TextEdit", textEditType);
            Assert.AreEqual("Line A\nLine B", typed);
            Assert.AreEqual("2", lineCount);
            Assert.AreEqual("Line B", secondLine);
            Assert.AreEqual("Picture event PictureArea(10,20).", statusBar.Text);
        }

        [TestMethod(DisplayName = "Verify that a box has no field children and a scroll container reports off-screen fields until it scrolls.")]
        public async Task BoxAndScrollContainerTestAsync()
        {
            // Arrange: open the shell controls test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_SHELLS");
            var scrollArea = await FindAsync(client, sessionId, ScrollAreaId);
            var lastLine = await FindAsync(client, sessionId, $"{ScrollAreaId}/txtZSIM_SHELLS-LINE25");

            // Act: read the box and its would-be children, then compare the last field's position before and after scrolling.
            var boxText = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/boxZSIM_SHELLS-ADDRESS"), "text");
            var boxChildren = await client.FindElementsAsync(sessionId, NewXpathLocator("//GuiBox[@name='ZSIM_SHELLS-ADDRESS']/*"));
            var city = await client.FindElementAsync(sessionId, NewIdLocator("wnd[0]/usr/txtZSIM_SHELLS-CITY"));
            var containerType = await client.GetAttributeAsync(sessionId, scrollArea, "type");
            var area = await client.GetRectAsync(sessionId, scrollArea);
            var before = await client.GetRectAsync(sessionId, lastLine);
            var maximum = await ReadAsync(client, sessionId, ScrollAreaId, "verticalScrollbar.maximum");
            await SetAsync(client, sessionId, ScrollAreaId, "verticalScrollbar.position", int.Parse(maximum, CultureInfo.InvariantCulture));
            var after = await client.GetRectAsync(sessionId, lastLine);

            // Assert: the box's fields are its siblings, and the last field moves from below the area into it.
            Assert.AreEqual("Address", boxText);
            Assert.IsEmpty(boxChildren);
            Assert.IsNotNull(city);
            Assert.AreEqual("GuiScrollContainer", containerType);
            Assert.IsGreaterThan(area.Y + area.Height, before.Y);
            Assert.IsLessThanOrEqualTo(area.Y + area.Height, after.Y + after.Height);
        }

        [TestMethod(DisplayName = "Verify that list labels follow paging and scrolling, F2 opens a secondary list, and Back returns to the same page.")]
        public async Task ListTestAsync()
        {
            // Arrange: open the list test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_LIST");
            const string flightLabel = "wnd[0]/usr/lbl[10,5]";

            // Act: read the first line, page down, drill down on the second line, go back, and scroll through the user area.
            var firstPage = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, flightLabel), "text");
            var charLeft = await ReadAsync(client, sessionId, $"/app/con[0]/ses[0]/{flightLabel}", "CharLeft");
            await client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 82, Window = "wnd[0]" });
            var secondPage = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, flightLabel), "text");
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/lbl[10,6]"));
            await client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 2, Window = "wnd[0]" });
            var detailFlight = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[0]/usr/lbl[20,4]"), "text");
            await client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 3, Window = "wnd[0]" });
            var afterBack = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, flightLabel), "text");
            await SetAsync(client, sessionId, $"{MainWindowId}/usr", "verticalScrollbar.position", 100);
            var scrolled = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, flightLabel), "text");

            // Assert: the same identifier shows rows 0, 18, 18 again, and 100; the secondary list showed row 19.
            Assert.AreEqual("0017", firstPage);
            Assert.AreEqual("10", charLeft);
            Assert.AreEqual("0089", secondPage);
            Assert.AreEqual("0093", detailFlight);
            Assert.AreEqual("0089", afterBack);
            Assert.AreEqual("0417", scrolled);
        }
    }
}
