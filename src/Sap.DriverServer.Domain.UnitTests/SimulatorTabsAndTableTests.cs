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
        // Identifies the note field on the main tab strip's first tab, inside that tab's subscreen.
        private const string GeneralNoteId = @"wnd[0]/usr/tabsTS_MAIN/tabpT\01/ssubSUB_MAIN:SAPMZSIM_TABS:0110/txtZSIM_TABS-NOTE";

        // Identifies the note field on the main tab strip's second tab, inside a different subscreen.
        private const string DetailsNoteId = @"wnd[0]/usr/tabsTS_MAIN/tabpT\02/ssubSUB_MAIN:SAPMZSIM_TABS:0120/txtZSIM_TABS-NOTE";

        // Identifies the item table control of the table test lab, relative to the session.
        private const string ItemTableId = "wnd[0]/usr/tblSAPMZSIM_TABLETC_ITEMS";

        [TestMethod(DisplayName = "Verify that a field on two tabs has a subscreen-specific identifier and only the selected tab's field exists.")]
        public async Task TabSubscreenTestAsync()
        {
            // Arrange: open the tab strip test lab and type a note on the first tab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_TABS");
            var generalNote = await FindAsync(client, sessionId, GeneralNoteId);
            await client.SetValueAsync(sessionId, generalNote, "first tab");

            // Act: switch to the second tab, look for both note identifiers, then switch back.
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, @"wnd[0]/usr/tabsTS_MAIN/tabpT\02"));
            var hiddenNote = await client.FindElementAsync(sessionId, NewIdLocator(GeneralNoteId));
            var detailsNote = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, DetailsNoteId), "text");
            var staleRead = await Assert.ThrowsExactlyAsync<SapDriverException>(() => client.GetAttributeAsync(sessionId, generalNote, "text"));
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, @"wnd[0]/usr/tabsTS_MAIN/tabpT\01"));
            var keptNote = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, GeneralNoteId), "text");

            // Assert: the hidden tab's field is gone, the other tab has its own empty note, and the entry survived.
            Assert.IsNull(hiddenNote);
            Assert.AreEqual(string.Empty, detailsNote);
            Assert.IsNotNull(staleRead);
            Assert.AreEqual("first tab", keptNote);
        }

        [TestMethod(DisplayName = "Verify that a check box inside nested tab strips is reachable by its full identifier and by an XPath name.")]
        public async Task NestedTabsTestAsync()
        {
            // Arrange: open the tab strip test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_TABS");
            const string deepId = @"wnd[0]/usr/tabsTS_MAIN/tabpT\03/ssubSUB_MAIN:SAPMZSIM_TABS:0130/tabsTS_INNER/tabpI\02/ssubSUB_INNER:SAPMZSIM_TABS:0132/chkZSIM_TABS-DEEP";

            // Act: look for the check box before and after selecting the outer and inner tabs, then select it.
            var before = await client.FindElementAsync(sessionId, NewXpathLocator("//GuiCheckBox[@name='ZSIM_TABS-DEEP']"));
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, @"wnd[0]/usr/tabsTS_MAIN/tabpT\03"));
            await client.ClickAsync(sessionId, await FindAsync(client, sessionId, @"wnd[0]/usr/tabsTS_MAIN/tabpT\03/ssubSUB_MAIN:SAPMZSIM_TABS:0130/tabsTS_INNER/tabpI\02"));
            var byXpath = await client.FindElementAsync(sessionId, NewXpathLocator("//GuiCheckBox[@name='ZSIM_TABS-DEEP']"));
            var deep = await FindAsync(client, sessionId, deepId);
            await client.ClickAsync(sessionId, deep);

            // Assert: the check box exists only once both tabs are selected, and both locators reach it.
            Assert.IsNull(before);
            Assert.IsNotNull(byXpath);
            Assert.AreEqual("true", await client.GetAttributeAsync(sessionId, deep, "selected"));
        }

        [TestMethod(DisplayName = "Verify that a table control exposes cells for its visible lines only and counts rows and lines separately.")]
        public async Task TableVisibleLinesTestAsync()
        {
            // Arrange: open the table test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_TABLE");
            var tableId = $"/app/con[0]/ses[0]/{ItemTableId}";

            // Act: read the counts, the first and last visible item cells, and look for a cell below the last line.
            var rowCount = await ReadAsync(client, sessionId, tableId, "RowCount");
            var visibleRowCount = await ReadAsync(client, sessionId, tableId, "VisibleRowCount");
            var firstCell = await FindAsync(client, sessionId, $"{ItemTableId}/txtITAB-POSNR[0,0]");
            var firstItem = await client.GetAttributeAsync(sessionId, firstCell, "text");
            var lastItem = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, $"{ItemTableId}/txtITAB-POSNR[0,11]"), "text");
            var belowLines = await client.FindElementAsync(sessionId, NewIdLocator($"{ItemTableId}/txtITAB-POSNR[0,12]"));
            var cellName = await client.GetAttributeAsync(sessionId, firstCell, "name");
            var cellChangeable = await client.GetAttributeAsync(sessionId, firstCell, "changeable");

            // Assert: 200 rows behind 12 lines, cells only for the lines, and SAP-style cell names.
            Assert.AreEqual("200", rowCount);
            Assert.AreEqual("12", visibleRowCount);
            Assert.AreEqual("000010", firstItem);
            Assert.AreEqual("000120", lastItem);
            Assert.IsNull(belowLines);
            Assert.AreEqual("ITAB-POSNR", cellName);
            Assert.AreEqual("false", cellChangeable, ignoreCase: true);
        }

        [TestMethod(DisplayName = "Verify that scrolling a table control keeps edits and makes the same cell identifiers show other rows.")]
        public async Task TableScrollTestAsync()
        {
            // Arrange: open the table test lab and edit the material of the first row.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_TABLE");
            var itemCell = await FindAsync(client, sessionId, $"{ItemTableId}/txtITAB-POSNR[0,0]");
            var materialCell = await FindAsync(client, sessionId, $"{ItemTableId}/ctxtITAB-MATNR[1,0]");
            await client.SetValueAsync(sessionId, materialCell, "EDITED-MAT");

            // Act: page down, jump to the last page, scroll to row 195 through the scroll bar, then back to the top.
            await client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 82, Window = "wnd[0]" });
            var afterPageDown = await client.GetAttributeAsync(sessionId, itemCell, "text");
            await client.SendVirtualKeyAsync(sessionId, new SapVirtualKeyModel { VirtualKey = 83, Window = "wnd[0]" });
            var afterLastPage = await client.GetAttributeAsync(sessionId, itemCell, "text");
            await SetAsync(client, sessionId, $"/app/con[0]/ses[0]/{ItemTableId}", "verticalScrollbar.position", 195);
            var lastLineItem = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, $"{ItemTableId}/txtITAB-POSNR[0,4]"), "text");
            var emptyLine = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, $"{ItemTableId}/txtITAB-POSNR[0,5]"), "changeable");
            await SetAsync(client, sessionId, $"/app/con[0]/ses[0]/{ItemTableId}", "verticalScrollbar.position", 0);
            var keptMaterial = await client.GetAttributeAsync(sessionId, materialCell, "text");

            // Assert: the first line followed the scroll position, lines past the end are locked, and the edit was kept.
            Assert.AreEqual("000130", afterPageDown);
            Assert.AreEqual("001890", afterLastPage);
            Assert.AreEqual("002000", lastLineItem);
            Assert.AreEqual("false", emptyLine, ignoreCase: true);
            Assert.AreEqual("EDITED-MAT", keptMaterial);
        }
    }
}
