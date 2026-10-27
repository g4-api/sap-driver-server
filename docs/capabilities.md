# Capability Mapping

This page maps every route of the UIA driver server to its SAP GUI Scripting equivalent in the SAP driver server.
Routes are served under both `/` and `/wd/hub/`.

## Sessions and status

| UIA route | SAP GUI Scripting equivalent | SAP driver behavior |
| --- | --- | --- |
| `POST /session` with `uia:options` | Attach to an existing `GuiSession` | Selects one session through `sap:options`; ambiguous or empty matches fail with `session not created` |
| `DELETE /session/{s}` | None | Detaches only; SAP windows stay open |
| `DELETE /session` | None | Detaches every session |
| `GET /status` | `GuiApplication.Children` | UIA shape plus the additive `sap` reachability member |
| `GET /session/{s}/window` | `GuiSession.ActiveWindow`, `Handle`, `Text` | JSON string `{"handle":"…","name":"…"}`, as in the UIA server |
| `GET /session/{s}/window/handles` | `GuiSession.Children` (`wnd[n]`) | Hexadecimal handles in window order |
| `POST /session/{s}/window` | `GuiFrameWindow.setFocus` | Matches a handle, a `wnd[n]` name, or an exact title |
| `POST /session/{s}/window/maximize` | `GuiFrameWindow.maximize` | Returns the window rectangle |
| `GET /session/{s}/screenshot` | `GuiFrameWindow.HardCopy` (PNG) | Base64 PNG of the current SAP window |

## Elements

| UIA route | SAP GUI Scripting equivalent | SAP driver behavior |
| --- | --- | --- |
| `POST /session/{s}/element(s)` | `findById`, a live-tree walk, or the object model document | `xpath`, `id`, `tag name`, simple `css selector`; simple XPath paths walk the live tree |
| `POST /session/{s}/element/{e}/element(s)` | Same, scoped to the parent | Descendants of the parent only |
| `POST /session/{s}/element/{e}/click` | `press`, `select`, `selected`, `setFocus` | Mapped per control type; see below |
| `POST /session/{s}/element/{e}/value` | `text`, combo box `key` | Replaces the text |
| `POST /session/{s}/element/{e}/clear` | `text = ""` | Text controls only |
| `GET /session/{s}/element/{e}/text` | `Text` | Empty for password fields |
| `GET /session/{s}/element/{e}/attribute/{n}` | Whitelisted properties | Empty string for unknown or unexposed names |
| `GET /session/{s}/element/{e}/rect` | `ScreenLeft`, `ScreenTop`, `Width`, `Height` | W3C `{x, y, width, height}` in screen pixels |
| `GET /session/{s}/source` | Object model document | XML string in `value` |
| `GET /session/{s}/dom`, `POST /session/{s}/element/dom` | Object model document | XML content, as in the UIA server |

### Click mapping

| SAP type | Scripting member |
| --- | --- |
| `GuiButton` | `press()` |
| `GuiTab`, `GuiRadioButton`, `GuiMenu` | `select()` |
| `GuiCheckBox` | `selected = !selected` |
| `GuiTextField`, `GuiCTextField`, `GuiPasswordField`, `GuiOkCodeField`, `GuiComboBox`, `GuiLabel` | `setFocus()` |
| `GuiShell` and every other type | `unsupported operation` |

### Attribute names

The attribute endpoint accepts these names, case-insensitively. The `left` and `top` names read `ScreenLeft` and
`ScreenTop`, and Booleans are returned as `true` or `false`. A name the control does not expose reads as an empty
string, and password text is never returned, through `text` or `displayedText`.

| Group | Names |
| --- | --- |
| Every control | `changeable`, `containerType`, `defaultTooltip`, `handle`, `height`, `highlighted`, `iconName`, `id`, `left`, `modified`, `name`, `subType`, `text`, `tooltip`, `top`, `type`, `width` |
| Fields and labels | `charLeft`, `charTop`, `displayedText`, `isListElement`, `isOField`, `key`, `maxLength`, `numerical`, `required`, `selected`, `value` |
| Grids, tables, and trees | `columnCount`, `currentCellColumn`, `currentCellRow`, `firstVisibleRow`, `rowCount`, `selectedItemNode`, `selectedNode`, `selectedRows`, `toolbarButtonCount`, `visibleRowCount` |
| Toolbars and text edits | `buttonCount`, `lineCount` |
| Status bar | `messageAsPopup` |
| Scroll bars | `verticalScrollbar.position`, `verticalScrollbar.maximum`, `verticalScrollbar.pageSize`, and the same three for `horizontalScrollbar` |

## Actions and keys

`POST /session/{s}/actions` converts key actions to `GuiFrameWindow.sendVKey` calls on the current window. The whole
payload is validated first, so an unsupported action sends nothing.

| W3C key | Modifier | SAP virtual key |
| --- | --- | ---: |
| Enter or Return | None | 0 |
| F1 to F12 | None | 1 to 12 |
| F1 to F12 | Shift | 13 to 24 |
| F1 to F12 | Ctrl | 25 to 36 |
| F1 to F12 | Ctrl+Shift | 37 to 48 |
| Escape | None | 12 |
| Page Up | None | 81 |
| Page Down | None | 82 |
| Page Up | Ctrl | 80 |
| Page Down | Ctrl | 83 |

Pause items are honored up to 10 seconds each. Character keys, Alt, Meta, Tab, arrows, and pointer or wheel sources
return `unsupported operation`. Use the element value endpoint to enter text, or `POST /session/{s}/sap/vkey` for any
other virtual key.

## SAP-only routes

| Route | Scripting member | Result |
| --- | --- | --- |
| `GET /sap/sessions` | `GuiApplication`, `GuiConnection`, `GuiSessionInfo` | Connections with session IDs, system, client, and description |
| `POST /session/{s}/sap/vkey` | `GuiFrameWindow.sendVKey` | Body `{"virtualKey": 3, "window": "wnd[0]"}`; `window` is optional |
| `GET /session/{s}/sap/statusbar` | `GuiStatusbar` (`wnd[0]/sbar`) | Message type, ID, number, text, and `messageAsPopup` |
| `POST /session/{s}/sap/element/at` | `GuiSession.findByPosition` | Body `{"x": 110, "y": 205}`; returns an element reference, `sapId`, `type`, `rect`, and `innerObject`, or `no such element` |
| `POST /session/{s}/sap/element/{e}/call` | Allow-listed control methods | Body `{"method": "pressToolbarButton", "arguments": ["&REFRESH"]}`; returns the method's result |
| `POST /session/{s}/sap/element/{e}/property` | Allow-listed control properties | Body `{"name": "selectedRows", "value": "2-4,7"}` |

These routes are SAP extensions with the `sap` path prefix; the W3C WebDriver standard has no commands for SAP shells.
Standard clients call them with a raw HTTP request or a registered custom command.

### Control methods and properties

The call and property routes accept only the methods and properties listed for the element's control family. The
family is the SAP type, refined by the subtype for shells. Any other name returns `unsupported operation` before SAP
GUI is called, so clients cannot reach scripting members that close sessions or change settings.

1. Method names are matched case-insensitively.
2. Arguments and values are JSON strings, numbers, Booleans, or null; whole numbers are passed as integers. A JSON
   object or array returns `invalid argument`.
3. Scalar results are returned as they are, collections as JSON arrays, and controls as element references that
   every element route accepts. A method without a result returns an empty object.
4. Argument values and results are not logged, because they can hold business data.

| Control family | Methods |
| --- | --- |
| Grid view shell (`GuiShell`, `GridView`) | `clearSelection`, `clickCurrentCell`, `contextMenu`, `doubleClickCurrentCell`, `getCellValue`, `getToolbarButtonId`, `getToolbarButtonText`, `getToolbarButtonType`, `pressToolbarButton`, `pressToolbarContextButton`, `selectAll`, `selectContextMenuItem`, `selectContextMenuItemByText`, `setCurrentCell` |
| Tree shell (`GuiShell`, `Tree`) | `collapseNode`, `doubleClickItem`, `doubleClickNode`, `expandNode`, `getItemText`, `getNodeChildrenCount`, `getNodeTextByKey`, `getParent`, `getTreeType`, `isFolder`, `selectItem`, `selectNode`; collections: `getAllNodeKeys`, `getColumnNames`, `getColumnTitles`, `getSubNodesCol` |
| Toolbar shell (`GuiShell`, `Toolbar`) | `getButtonChecked`, `getButtonEnabled`, `getButtonId`, `getButtonText`, `getButtonTooltip`, `getButtonType`, `pressButton`, `pressContextButton`, `selectContextMenuItem` |
| Text edit shell (`GuiShell`, `TextEdit`) | `getLineText`, `setSelectionIndexes` |
| Picture shell (`GuiShell`, `Picture`) | `click`, `clickPictureArea`, `doubleClick` |
| Table control (`GuiTableControl`) | `getCell`, which returns an element reference |

| Control family | Settable properties |
| --- | --- |
| Grid view shell | `currentCellColumn`, `currentCellRow`, `firstVisibleRow`, `selectedRows` |
| Table control, scroll container, user area | `verticalScrollbar.position`, `horizontalScrollbar.position` |

## Not supported

| UIA route or feature | Reason |
| --- | --- |
| `POST /session/{s}/execute/sync` | SAP GUI Scripting offers no script host to the driver |
| `/session/{s}/user32/*` | Raw Windows input bypasses SAP GUI Scripting |
| `GET /scancodes` | Keyboard scan codes are User32-specific |
| OCR and coordinate locators | Screen-based matching is not a scripting identifier |
| Grid cells, tree nodes, and toolbar buttons as elements | Shell internals are methods, not scripting objects; use the call route |
| Events and recording | The UIA server has no event contract to mirror |
