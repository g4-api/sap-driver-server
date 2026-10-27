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

namespace Sap.DriverServer.Domain.UnitTests
{
    public partial class SimulatorEndToEndTests
    {
        // Identifies the ALV grid of the ALV test lab.
        private const string AlvGridId = MainWindowId + "/usr/cntlALV_CONTAINER/shellcont/shell";

        // Identifies the column tree of the tree test lab.
        private const string ColumnTreeId = MainWindowId + "/usr/cntlTREE_COLUMN/shellcont/shell";

        // Lists the column names the column tree reports, in display order.
        private static readonly string[] ExpectedColumnNames = ["HIER", "PRICE", "SEATS"];

        // Lists the tree types of the simple, list, and column trees, as the driver returns them.
        private static readonly string[] ExpectedTreeTypes = ["0", "1", "2"];

        // Identifies the list tree of the tree test lab.
        private const string ListTreeId = MainWindowId + "/usr/cntlTREE_LIST/shellcont/shell";

        // Identifies the simple tree of the tree test lab.
        private const string SimpleTreeId = MainWindowId + "/usr/cntlTREE_SIMPLE/shellcont/shell";

        [TestMethod(DisplayName = "Verify that an ALV grid reads unloaded rows as empty until firstVisibleRow brings them into view.")]
        public async Task AlvLazyRowsTestAsync()
        {
            // Arrange: open the ALV test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_ALV");

            // Act: read a visible and a far row, scroll the far row into view, and read it again.
            var rowCount = await ReadAsync(client, sessionId, AlvGridId, "RowCount");
            var visibleRowCount = int.Parse(await ReadAsync(client, sessionId, AlvGridId, "VisibleRowCount"));
            var firstRow = await CallAsync(client, sessionId, AlvGridId, "getCellValue", 0, "CARRID");
            var unloaded = await CallAsync(client, sessionId, AlvGridId, "getCellValue", 400, "CONNID");
            await SetAsync(client, sessionId, AlvGridId, "firstVisibleRow", 400);
            var firstVisibleRow = await ReadAsync(client, sessionId, AlvGridId, "FirstVisibleRow");
            var loaded = await CallAsync(client, sessionId, AlvGridId, "getCellValue", 400, "CONNID");

            // Assert: 500 rows behind a few visible ones, and row 400 has a value only after it was shown.
            Assert.AreEqual("500", rowCount);
            Assert.IsInRange(1, 499, visibleRowCount);
            Assert.AreEqual("SIM", firstRow);
            Assert.AreEqual(string.Empty, unloaded);
            Assert.AreEqual("400", firstVisibleRow);
            Assert.AreEqual("1617", loaded);
        }

        [TestMethod(DisplayName = "Verify that ALV toolbar buttons are pressed by function code and menu buttons need a context menu selection.")]
        public async Task AlvToolbarTestAsync()
        {
            // Arrange: open the ALV test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_ALV");

            // Act: read the toolbar, press the menu button the wrong way, then open its menu and pick an item.
            var buttonCount = await ReadAsync(client, sessionId, AlvGridId, "ToolbarButtonCount");
            var exportType = await CallAsync(client, sessionId, AlvGridId, "getToolbarButtonType", 4);
            await Assert.ThrowsAsync<SapDriverException>(() => CallAsync(client, sessionId, AlvGridId, "pressToolbarButton", "&MB_EXPORT"));
            await CallAsync(client, sessionId, AlvGridId, "pressToolbarContextButton", "&MB_EXPORT");
            await CallAsync(client, sessionId, AlvGridId, "selectContextMenuItem", "&XXL");
            var statusBar = await client.GetStatusBarAsync(sessionId);
            var secondSelection = await Assert.ThrowsAsync<SapDriverException>(() => CallAsync(client, sessionId, AlvGridId, "selectContextMenuItem", "&XXL"));

            // Assert: five buttons with one menu button, the export ran once, and the closed menu refuses a second pick.
            Assert.AreEqual("5", buttonCount);
            Assert.AreEqual("Menu", exportType);
            Assert.AreEqual(("S", "073"), (statusBar.MessageType, statusBar.MessageNumber));
            Assert.IsNotNull(secondSelection);
        }

        [TestMethod(DisplayName = "Verify that ALV sorting moves rows, ranges select rows, hotspots fire, and a double click opens details.")]
        public async Task AlvRowsTestAsync()
        {
            // Arrange: open the ALV test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_ALV");

            // Act: read row 1, sort ascending by airline, select a range, click a hotspot, and double-click a row.
            var beforeSort = await CallAsync(client, sessionId, AlvGridId, "getCellValue", 1, "CARRID");
            await CallAsync(client, sessionId, AlvGridId, "pressToolbarButton", "&SORT_ASC");
            var afterSort = await CallAsync(client, sessionId, AlvGridId, "getCellValue", 1, "CARRID");
            await SetAsync(client, sessionId, AlvGridId, "selectedRows", "2-4,7");
            var selectedRows = await ReadAsync(client, sessionId, AlvGridId, "SelectedRows");
            await CallAsync(client, sessionId, AlvGridId, "setCurrentCell", 0, "CONNID");
            await CallAsync(client, sessionId, AlvGridId, "clickCurrentCell");
            var hotspot = await client.GetStatusBarAsync(sessionId);
            await CallAsync(client, sessionId, AlvGridId, "doubleClickCurrentCell");
            await client.SwitchToWindowAsync(sessionId, "wnd[1]");
            var detailAirline = await client.GetAttributeAsync(sessionId, await FindAsync(client, sessionId, "wnd[1]/usr/txtDETAIL-CARRID"), "text");
            await SendEnterAsync(client, sessionId);

            // Assert: the same row index names another airline after sorting, and each action reached the screen.
            Assert.AreEqual("TST", beforeSort);
            Assert.AreEqual("DMO", afterSort);
            Assert.AreEqual("2,3,4,7", selectedRows);
            Assert.AreEqual(("S", "075"), (hotspot.MessageType, hotspot.MessageNumber));
            Assert.AreEqual("DMO", detailAirline);
        }

        [TestMethod(DisplayName = "Verify that tree types, padded node keys, and lazy folders behave like SAP tree controls.")]
        public async Task TreesTestAsync()
        {
            // Arrange: open the tree test lab.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_TREES");
            var root = Pad(1);

            // Act: read the types and keys, try an unpadded key, then expand the root and an airline folder.
            var types = new List<string>();
            foreach (var tree in new[] { SimpleTreeId, ListTreeId, ColumnTreeId })
            {
                types.Add(await CallAsync(client, sessionId, tree, "getTreeType"));
            }

            var keysBefore = await CallListAsync(client, sessionId, SimpleTreeId, "getAllNodeKeys");
            var unpadded = await Assert.ThrowsAsync<SapDriverException>(() => CallAsync(client, sessionId, SimpleTreeId, "getNodeTextByKey", "1"));
            var childBefore = await Assert.ThrowsAsync<SapDriverException>(() => CallAsync(client, sessionId, SimpleTreeId, "getNodeTextByKey", Pad(5)));
            await CallAsync(client, sessionId, SimpleTreeId, "expandNode", root);
            await CallAsync(client, sessionId, SimpleTreeId, "expandNode", Pad(2));
            var keysAfter = await CallListAsync(client, sessionId, SimpleTreeId, "getAllNodeKeys");
            var flightText = await CallAsync(client, sessionId, SimpleTreeId, "getNodeTextByKey", Pad(5));

            // Assert: the three types, a root-only key list before expansion, and exact padded keys.
            Assert.AreSequenceEqual(ExpectedTreeTypes, types);
            Assert.AreSequenceEqual(new[] { root }, keysBefore);
            Assert.IsNotNull(unpadded);
            Assert.IsNotNull(childBefore);
            Assert.HasCount(7, keysAfter);
            Assert.AreEqual("SIM 0017", flightText);
        }

        [TestMethod(DisplayName = "Verify that list and column trees expose texts through items and report item double clicks.")]
        public async Task TreeItemsTestAsync()
        {
            // Arrange: open the tree test lab and expand the first airline in the list and column trees.
            using var simulator = SimulatorProcess.Start();
            using var factory = new WebApplicationFactory<Program>();
            using var client = TestDrivers.NewClient(factory);
            var sessionId = await NewLoggedOnSessionAsync(client);
            await RunCommandAsync(client, sessionId, "/nZSIM_TREES");
            foreach (var tree in new[] { ListTreeId, ColumnTreeId })
            {
                await CallAsync(client, sessionId, tree, "expandNode", Pad(1));
                await CallAsync(client, sessionId, tree, "expandNode", Pad(2));
            }

            // Act: read the columns and item texts, then double-click a column tree item.
            var columns = await CallListAsync(client, sessionId, ColumnTreeId, "getColumnNames");
            var price = await CallAsync(client, sessionId, ColumnTreeId, "getItemText", Pad(5), "PRICE");
            var seats = await CallAsync(client, sessionId, ListTreeId, "getItemText", Pad(5), "3");
            await CallAsync(client, sessionId, ColumnTreeId, "doubleClickItem", Pad(5), "PRICE");
            var statusBar = await client.GetStatusBarAsync(sessionId);
            var itemNode = await ReadAsync(client, sessionId, ColumnTreeId, "SelectedItemNode");

            // Assert: column names, item texts by name, and the item double click with its node.
            Assert.AreSequenceEqual(ExpectedColumnNames, columns);
            Assert.AreEqual("150.00", price);
            Assert.AreEqual("10", seats);
            Assert.AreEqual($"Item 'PRICE' of node '{Pad(5)}' double-clicked.", statusBar.Text);
            Assert.AreEqual(Pad(5), itemNode);
        }

        // Pads a node number the way the simulator's tree keys are padded: right-aligned to ten characters.
        private static string Pad(int number)
        {
            return number.ToString(CultureInfo.InvariantCulture).PadLeft(10);
        }
    }
}
