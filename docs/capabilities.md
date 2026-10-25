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

The attribute endpoint accepts these names, case-insensitively: `changeable`, `containerType`, `defaultTooltip`,
`handle`, `height`, `highlighted`, `iconName`, `id`, `key`, `left`, `maxLength`, `modified`, `name`, `numerical`,
`required`, `selected`, `subType`, `text`, `tooltip`, `top`, `type`, `value`, and `width`. The `left` and `top`
names read `ScreenLeft` and `ScreenTop`, and Booleans are returned as `true` or `false`.

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
| `GET /session/{s}/sap/statusbar` | `GuiStatusbar` (`wnd[0]/sbar`) | Message type, ID, number, and text |
| `POST /session/{s}/sap/element/at` | `GuiSession.findByPosition` | Body `{"x": 110, "y": 205}`; returns an element reference, `sapId`, `type`, `rect`, and `innerObject`, or `no such element` |

## Not supported

| UIA route or feature | Reason |
| --- | --- |
| `POST /session/{s}/execute/sync` | SAP GUI Scripting offers no script host to the driver |
| `/session/{s}/user32/*` | Raw Windows input bypasses SAP GUI Scripting |
| `GET /scancodes` | Keyboard scan codes are User32-specific |
| OCR and coordinate locators | Screen-based matching is not a scripting identifier |
| Grid cells, tree nodes, and toolbar buttons inside shells | Shell internals are methods, not scripting objects |
| Events and recording | The UIA server has no event contract to mirror |
