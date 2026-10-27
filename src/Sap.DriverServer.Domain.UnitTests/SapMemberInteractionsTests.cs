using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Sap.DriverServer.Domain.UnitTests.Support;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain.UnitTests
{
    [TestClass]
    [OSCondition(OperatingSystems.Windows)]
    [TestCategory("SapMemberInteractions")]
    [TestCategory("UnitTest")]
    public class SapMemberInteractionsTests
    {
        // Lists the invocation the getCellValue call records, with its converted arguments.
        private static readonly string[] ExpectedCellValueCall = ["getCellValue(3,CONNID)"];

        // Lists the scroll bar assignment the dotted property records.
        private static readonly string[] ExpectedPositionSet = ["set Position=24"];

        // Lists the grid assignment the selectedRows property records.
        private static readonly string[] ExpectedSelectedRowsSet = ["set SelectedRows=2-4,7"];

        // Lists the node keys the fake tree method returns.
        private static readonly string[] NodeKeys = ["         1", "         2"];

        // Marks a converted control result, so tests can tell the converter ran.
        private const string ReferenceMarker = "reference";

        [TestMethod(DisplayName = "Verify that an allow-listed method runs with JSON arguments converted to integers and strings.")]
        public void InvokeMethodConvertsArgumentsTest()
        {
            // Arrange: a grid view shell whose getCellValue returns a value, and JSON arguments as the driver binds them.
            var grid = NewShell("GridView");
            grid.Methods["getCellValue"] = _ => "0029";
            var call = new SapMethodCallModel { Arguments = ParseArguments("[3, \"CONNID\"]"), Method = "getCellValue" };

            // Act: call the method.
            var result = SapMemberInteractions.InvokeMethod(grid, call, _ => ReferenceMarker);

            // Assert: the integer and string reached SAP, and the scalar result came back.
            Assert.AreEqual("0029", result);
            Assert.AreSequenceEqual(ExpectedCellValueCall, grid.Invocations);
        }

        [TestMethod(DisplayName = "Verify that methods outside the control family's allow-list are refused before SAP is called.")]
        #region *** Data Set ***
        [DataRow("GuiShell", "GridView", "close")]
        [DataRow("GuiShell", "Tree", "pressToolbarButton")]
        [DataRow("GuiButton", "", "press")]
        [DataRow("GuiShell", "Unknown", "click")]
        #endregion
        public void InvokeMethodRefusedTest(string type, string subType, string method)
        {
            // Arrange: a control of the family and a call to a method that family does not allow.
            var node = new FakeSapGuiNode("/app/con[0]/ses[0]/wnd[0]/usr/ctrl", type).With("SubType", subType);
            var call = new SapMethodCallModel { Method = method };

            // Act: call the method.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SapMemberInteractions.InvokeMethod(node, call, _ => ReferenceMarker));

            // Assert: an unsupported operation without a SAP error kind, and nothing reached SAP.
            Assert.AreEqual(WebDriverErrorCodes.UnsupportedOperation, exception.Error);
            Assert.IsNull(exception.ErrorKind);
            Assert.IsEmpty(node.Invocations);
        }

        [TestMethod(DisplayName = "Verify that a JSON object or array argument is an invalid argument and SAP is not called.")]
        #region *** Data Set ***
        [DataRow("[{\"a\": 1}]")]
        [DataRow("[[1, 2]]")]
        #endregion
        public void InvokeMethodInvalidArgumentTest(string json)
        {
            // Arrange: a tree shell call with a structured argument.
            var tree = NewShell("Tree");
            var call = new SapMethodCallModel { Arguments = ParseArguments(json), Method = "expandNode" };

            // Act: call the method.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SapMemberInteractions.InvokeMethod(tree, call, _ => ReferenceMarker));

            // Assert: the request is rejected as invalid, and nothing reached SAP.
            Assert.AreEqual(WebDriverErrorCodes.InvalidArgument, exception.Error);
            Assert.IsEmpty(tree.Invocations);
        }

        [TestMethod(DisplayName = "Verify that collection methods return arrays and control results go through the converter.")]
        public void InvokeMethodResultShapesTest()
        {
            // Arrange: a tree that returns node keys and a table control that returns a cell control.
            var tree = NewShell("Tree");
            tree.Methods["getAllNodeKeys"] = _ => NodeKeys;
            var table = new FakeSapGuiNode("/app/con[0]/ses[0]/wnd[0]/usr/tblITEMS", "GuiTableControl");
            var cell = new FakeSapGuiNode("/app/con[0]/ses[0]/wnd[0]/usr/tblITEMS/txtITAB-POSNR[0,1]", "GuiTextField");
            table.Methods["getCell"] = _ => cell;
            ISapGuiNode converted = null;

            // Act: read the keys and the cell.
            var keys = SapMemberInteractions.InvokeMethod(tree, new SapMethodCallModel { Method = "GETALLNODEKEYS" }, _ => ReferenceMarker);
            var reference = SapMemberInteractions.InvokeMethod(table, new SapMethodCallModel { Arguments = [1, 0], Method = "getCell" }, i =>
            {
                converted = i;
                return ReferenceMarker;
            });

            // Assert: the keys are an array with their padding, and the cell was handed to the converter.
            Assert.AreSequenceEqual(NodeKeys, ((IEnumerable<object>)keys).Cast<string>());
            Assert.AreEqual(ReferenceMarker, reference);
            Assert.AreSame(cell, converted);
        }

        [TestMethod(DisplayName = "Verify that allow-listed properties are set directly or on the scroll bar a dotted name names.")]
        public void SetPropertyTest()
        {
            // Arrange: a grid view shell and a table control with a vertical scroll bar.
            var grid = NewShell("GridView");
            var table = new FakeSapGuiNode("/app/con[0]/ses[0]/wnd[0]/usr/tblITEMS", "GuiTableControl");
            var scrollbar = new FakeSapGuiNode("/app/con[0]/ses[0]/wnd[0]/usr/tblITEMS/scrollbar", "GuiScrollbar");
            table.Properties["VerticalScrollbar"] = scrollbar;
            using var position = JsonDocument.Parse("24");

            // Act: select grid rows and scroll the table with a JSON number.
            SapMemberInteractions.SetProperty(grid, new SapPropertyModel { Name = "selectedRows", Value = "2-4,7" });
            SapMemberInteractions.SetProperty(table, new SapPropertyModel { Name = "verticalScrollbar.Position", Value = position.RootElement.Clone() });

            // Assert: the grid property and the scroll bar position were assigned with SAP's names.
            Assert.AreSequenceEqual(ExpectedSelectedRowsSet, grid.Invocations);
            Assert.AreSequenceEqual(ExpectedPositionSet, scrollbar.Invocations);
        }

        [TestMethod(DisplayName = "Verify that properties outside the control family's allow-list are refused.")]
        #region *** Data Set ***
        [DataRow("GuiShell", "GridView", "text")]
        [DataRow("GuiTextField", "", "verticalScrollbar.position")]
        #endregion
        public void SetPropertyRefusedTest(string type, string subType, string name)
        {
            // Arrange: a control of the family and a property that family does not allow.
            var node = new FakeSapGuiNode("/app/con[0]/ses[0]/wnd[0]/usr/ctrl", type).With("SubType", subType);

            // Act: set the property.
            var exception = Assert.ThrowsExactly<SapDriverException>(() => SapMemberInteractions.SetProperty(node, new SapPropertyModel { Name = name, Value = "x" }));

            // Assert: an unsupported operation, and nothing was assigned.
            Assert.AreEqual(WebDriverErrorCodes.UnsupportedOperation, exception.Error);
            Assert.IsEmpty(node.Invocations);
        }

        // Creates a shell control of a subtype.
        private static FakeSapGuiNode NewShell(string subType)
        {
            return new FakeSapGuiNode("/app/con[0]/ses[0]/wnd[0]/usr/cntlC/shellcont/shell", "GuiShell").With("SubType", subType);
        }

        // Parses a JSON array into the argument list the driver's model binding produces: one JSON element per item.
        private static List<object> ParseArguments(string json)
        {
            // Clone each element, so the values outlive the parsed document.
            using var document = JsonDocument.Parse(json);

            return document.RootElement.EnumerateArray().Select(i => (object)i.Clone()).ToList();
        }
    }
}
