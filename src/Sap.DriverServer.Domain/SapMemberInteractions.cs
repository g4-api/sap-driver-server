using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Calls allow-listed SAP GUI Scripting methods and sets allow-listed properties on controls that the W3C element
    /// commands cannot drive, such as grid view, tree, and toolbar shells.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every method and property is allowed per control family: the SAP type, refined by the shell subtype for
    /// <c>GuiShell</c>, as in <c>GuiShell/GridView</c>. A name outside the family's list fails with
    /// "unsupported operation" before SAP GUI is called, so clients cannot reach arbitrary scripting members, such as
    /// members that close sessions or change system settings.
    /// </para>
    /// <para>
    /// Arguments and values are JSON scalars; whole numbers become integers. Results are returned as JSON-friendly
    /// values: scalars as they are, collections as arrays, and controls as element references created by the caller.
    /// Argument and result values are never logged or quoted in error messages, because they can hold business data.
    /// </para>
    /// </remarks>
    public static class SapMemberInteractions
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies a method that returns a <c>GuiCollection</c>, returned to the client as a JSON array.
        /// </summary>
        public const string CollectionResult = "collection";

        /// <summary>
        /// Specifies a method that returns another control, returned to the client as an element reference.
        /// </summary>
        public const string ElementResult = "element";

        /// <summary>
        /// Specifies a method that returns a scalar or nothing.
        /// </summary>
        public const string ValueResult = "value";

        // Names the grid view shell family.
        private const string GridViewFamily = "GuiShell/GridView";

        // Names the scroll container family; the user area is a scroll container too.
        private const string ScrollContainerFamily = "GuiScrollContainer";

        // Names the table control family.
        private const string TableControlFamily = "GuiTableControl";

        // Names the user area family, which scrolls like a scroll container.
        private const string UserAreaFamily = "GuiUserArea";

        // Lists the scroll bar positions every scrollable family accepts, as dotted public names and SAP paths.
        // Declared before the allow-lists, because static fields initialize in declaration order and they copy it.
        private static readonly (string Name, string Path)[] ScrollbarProperties =
        [
            ("horizontalScrollbar.position", "HorizontalScrollbar.Position"),
            ("verticalScrollbar.position", "VerticalScrollbar.Position")
        ];

        // Maps each control family to its allowed methods and the kind of result each one returns.
        private static readonly Dictionary<string, Dictionary<string, string>> AllowedMethods = new(StringComparer.Ordinal)
        {
            [GridViewFamily] = NewMethods(
                ValueResult,
                "clearSelection",
                "clickCurrentCell",
                "contextMenu",
                "doubleClickCurrentCell",
                "getCellValue",
                "getToolbarButtonId",
                "getToolbarButtonText",
                "getToolbarButtonType",
                "pressToolbarButton",
                "pressToolbarContextButton",
                "selectAll",
                "selectContextMenuItem",
                "selectContextMenuItemByText",
                "setCurrentCell"),
            ["GuiShell/Picture"] = NewMethods(ValueResult, "click", "clickPictureArea", "doubleClick"),
            ["GuiShell/TextEdit"] = NewMethods(ValueResult, "getLineText", "setSelectionIndexes"),
            ["GuiShell/Toolbar"] = NewMethods(
                ValueResult,
                "getButtonChecked",
                "getButtonEnabled",
                "getButtonId",
                "getButtonText",
                "getButtonTooltip",
                "getButtonType",
                "pressButton",
                "pressContextButton",
                "selectContextMenuItem"),
            ["GuiShell/Tree"] = MergeMethods(
                NewMethods(
                    ValueResult,
                    "collapseNode",
                    "doubleClickItem",
                    "doubleClickNode",
                    "expandNode",
                    "getItemText",
                    "getNodeChildrenCount",
                    "getNodeTextByKey",
                    "getParent",
                    "getTreeType",
                    "isFolder",
                    "selectItem",
                    "selectNode"),
                NewMethods(CollectionResult, "getAllNodeKeys", "getColumnNames", "getColumnTitles", "getSubNodesCol")),
            [TableControlFamily] = NewMethods(ElementResult, "getCell")
        };

        // Maps each control family to its settable properties: public names to SAP property paths.
        private static readonly Dictionary<string, Dictionary<string, string>> AllowedProperties = new(StringComparer.Ordinal)
        {
            [GridViewFamily] = NewProperties(
                [
                    ("currentCellColumn", "CurrentCellColumn"),
                    ("currentCellRow", "CurrentCellRow"),
                    ("firstVisibleRow", "FirstVisibleRow"),
                    ("selectedRows", "SelectedRows")
                ]),
            [ScrollContainerFamily] = NewProperties(ScrollbarProperties),
            [TableControlFamily] = NewProperties(ScrollbarProperties),
            [UserAreaFamily] = NewProperties(ScrollbarProperties)
        };
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the control family of a node: its type, refined by the subtype for shells.
        /// </summary>
        /// <param name="node">The control node.</param>
        /// <returns>The family, such as <c>GuiTableControl</c> or <c>GuiShell/GridView</c>.</returns>
        public static string GetFamily(ISapGuiNode node)
        {
            // Shells share one type, so their subtype decides which members they have.
            ArgumentNullException.ThrowIfNull(argument: node);

            var type = node.Type;

            return string.Equals(type, SapGuiTypes.Shell, StringComparison.Ordinal)
                ? $"{type}/{SapGuiNavigator.GetString(node, propertyName: "SubType")}"
                : type;
        }

        /// <summary>
        /// Calls an allow-listed method on a control.
        /// </summary>
        /// <param name="node">The control node.</param>
        /// <param name="call">The method name and positional arguments.</param>
        /// <param name="convertNode">Converts a control result into the value returned to the client, such as an element reference.</param>
        /// <returns>The JSON-friendly result: a scalar, an array, an element reference, or null.</returns>
        /// <exception cref="SapDriverException">The method is not allowed for the family, or an argument is not a scalar.</exception>
        public static object InvokeMethod(ISapGuiNode node, SapMethodCallModel call, Func<ISapGuiNode, object> convertNode)
        {
            // Assert the inputs before the allow-list is consulted.
            AssertArguments(node, call, convertNode);

            // Resolve the method against the family's list, then convert the arguments to COM-friendly scalars.
            var family = GetFamily(node);
            var methodName = call.Method?.Trim() ?? string.Empty;
            var methods = AllowedMethods.GetValueOrDefault(family);
            if (methods is null || !methods.TryGetValue(methodName, out var resultKind))
            {
                throw NewUnsupportedException($"The method '{methodName}' is not available for SAP control family '{family}'.");
            }

            var arguments = ConvertToArguments(call.Arguments);

            // Run the method and shape its result by kind; controls become whatever the caller issues for them.
            if (resultKind == CollectionResult)
            {
                return node
                    .InvokeCollectionMethod(methodName, arguments)
                    .Select(i => ConvertToResult(i, convertNode))
                    .ToList();
            }

            var result = resultKind == ElementResult
                ? node.InvokeNodeMethod(methodName, arguments)
                : node.InvokeMethod(methodName, arguments);

            return ConvertToResult(result, convertNode);

            // Asserts the node, the call, and the converter before SAP GUI is contacted; nothing is mutated, and a
            // missing argument fails the request as an ArgumentNullException, which the error handler reports.
            static void AssertArguments(ISapGuiNode node, SapMethodCallModel call, Func<ISapGuiNode, object> convertNode)
            {
                // Require every input; the method name itself is validated against the allow-list.
                ArgumentNullException.ThrowIfNull(argument: node);
                ArgumentNullException.ThrowIfNull(argument: call);
                ArgumentNullException.ThrowIfNull(argument: convertNode);
            }
        }

        /// <summary>
        /// Sets an allow-listed property on a control; dotted names set a property of a child object, such as a scroll bar.
        /// </summary>
        /// <param name="node">The control node.</param>
        /// <param name="property">The property name and value.</param>
        /// <exception cref="SapDriverException">The property is not allowed for the family, or the value is not a scalar.</exception>
        public static void SetProperty(ISapGuiNode node, SapPropertyModel property)
        {
            // Assert the inputs before the allow-list is consulted.
            AssertArguments(node, property);

            // Resolve the public name to its SAP path; names outside the family's list are refused.
            var family = GetFamily(node);
            var name = property.Name?.Trim() ?? string.Empty;
            var properties = AllowedProperties.GetValueOrDefault(family);
            if (properties is null || !properties.TryGetValue(name, out var path))
            {
                throw NewUnsupportedException($"The property '{name}' cannot be set on SAP control family '{family}'.");
            }

            // Assign the value on the control, or on the child object a dotted path names.
            var value = ConvertToArgument(property.Value, position: 0);
            var separator = path.IndexOf('.', StringComparison.Ordinal);
            if (separator < 0)
            {
                node.SetProperty(path, value);
                return;
            }

            var ownerName = path[..separator];
            var owner = node.GetNode(ownerName)
                ?? throw SapDriverException.NewNotExposed($"SAP GUI Scripting does not expose '{ownerName}' on this object.");
            owner.SetProperty(path[(separator + 1)..], value);

            return;

            // Asserts the node and the property request before SAP GUI is contacted; nothing is mutated, and a missing
            // argument fails the request as an ArgumentNullException, which the error handler reports.
            static void AssertArguments(ISapGuiNode node, SapPropertyModel property)
            {
                // Require both inputs; the property name itself is validated against the allow-list.
                ArgumentNullException.ThrowIfNull(argument: node);
                ArgumentNullException.ThrowIfNull(argument: property);
            }
        }

        // Converts one request value into a COM-friendly scalar; JSON arrays and objects are refused.
        private static object ConvertToArgument(object value, int position)
        {
            // Pass plain scalars through; JSON values from the request body are unwrapped by kind.
            if (value is not JsonElement element)
            {
                return value is null or string or bool or int or long or double
                    ? value
                    : throw NewInvalidArgumentException(position);
            }

            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.Number when element.TryGetInt32(out var number) => number,
                JsonValueKind.Number when element.TryGetInt64(out var number) => number,
                JsonValueKind.Number => element.GetDouble(),
                _ => throw NewInvalidArgumentException(position)
            };
        }

        // Converts the positional request arguments into COM-friendly scalars.
        private static object[] ConvertToArguments(IList<object> arguments)
        {
            // Treat a missing list as no arguments, and convert each value with its position for the error message.
            return (arguments ?? []).Select(ConvertToArgument).ToArray();
        }

        // Converts a result into a JSON-friendly value; controls go through the caller's converter.
        private static object ConvertToResult(object value, Func<ISapGuiNode, object> convertNode)
        {
            // Keep scalars; scripting objects other than controls are never serialized, so their internals cannot leak.
            return value switch
            {
                null => null,
                ISapGuiNode node => convertNode(node),
                string or bool => value,
                int or long or short or byte or double or float or decimal => value,
                _ => null
            };
        }

        // Merges method lists of different result kinds into one family list.
        private static Dictionary<string, string> MergeMethods(Dictionary<string, string> first, Dictionary<string, string> second)
        {
            // Copy both lists into one dictionary; the lists name distinct methods.
            var merged = new Dictionary<string, string>(first, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, resultKind) in second)
            {
                merged[name] = resultKind;
            }

            return merged;
        }

        // Builds the "invalid argument" failure for a request value that is not a JSON scalar.
        private static SapDriverException NewInvalidArgumentException(int position)
        {
            // Name only the position; the value itself may hold business data.
            var message = string.Create(CultureInfo.InvariantCulture, $"Argument {position} must be a string, number, Boolean, or null.");

            return new SapDriverException(
                WebDriverErrorCodes.InvalidArgument,
                errorKind: null,
                StatusCodes.Status400BadRequest,
                message);
        }

        // Builds a method list in which every method returns the same kind of result; names match case-insensitively.
        private static Dictionary<string, string> NewMethods(string resultKind, params string[] names)
        {
            return names.ToDictionary(i => i, _ => resultKind, StringComparer.OrdinalIgnoreCase);
        }

        // Builds a property list of public names and SAP paths; names match case-insensitively.
        private static Dictionary<string, string> NewProperties(IEnumerable<(string Name, string Path)> properties)
        {
            return properties.ToDictionary(i => i.Name, i => i.Path, StringComparer.OrdinalIgnoreCase);
        }

        // Builds the "unsupported operation" failure for a member outside the allow-list; it is driver policy, not SAP's.
        private static SapDriverException NewUnsupportedException(string message)
        {
            return new SapDriverException(
                WebDriverErrorCodes.UnsupportedOperation,
                errorKind: null,
                StatusCodes.Status500InternalServerError,
                message);
        }
        #endregion
    }
}
