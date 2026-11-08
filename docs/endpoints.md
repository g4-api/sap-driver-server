# Endpoints

This page lists every route of the SAP driver server. Each route is served under both `/` and `/wd/hub/`. The
summaries are quoted from the Swagger annotations in the controllers. Open `/swagger` on a running server for the
interactive OpenAPI document.

For the mapping to SAP GUI Scripting members, see [Capability mapping](capabilities.md). For error kinds, see
[Troubleshooting](troubleshooting.md).

## How to read this page

1. `{s}` is the WebDriver session id and `{e}` is an element reference, both returned by earlier calls.
2. The Source column gives the `[Route]` line, relative to `src\Sap.DriverServer\Controllers`.
3. Request bodies use camelCase names. The server sets `JsonNamingPolicy.CamelCase` in `Program.cs:49`.
4. Responses carry the result in the W3C `value` member (`Sap.DriverServer.Models\WebDriverResponseModel.cs:51`).
   Errors carry `error`, `message` and, for SAP or COM failures, `data.sapErrorKind`
   (`Sap.DriverServer.Models\WebDriverErrorModel.cs:21-31`).

## Server and sessions

| Verb | Route | Summary | Request body | Source |
| --- | --- | --- | --- | --- |
| `GET` | `/status` | Gets the server status. | None | `SessionsController.cs:163` |
| `POST` | `/session` | Attaches a new session to an open SAP GUI session. | `NewSessionModel` with `sap:options` | `SessionsController.cs:260` |
| `DELETE` | `/session/{s}` | Detaches the specified session. | None | `SessionsController.cs:31` |
| `DELETE` | `/session` | Detaches all sessions. | None | `SessionsController.cs:61` |
| `GET` | `/session/{s}/window` | Gets the current window handle. | None | `SessionsController.cs:204` |
| `GET` | `/session/{s}/window/handles` | Gets all window handles. | None | `SessionsController.cs:234` |
| `POST` | `/session/{s}/window` | Switches to a window. | `{"handle": "..."}` | `SessionsController.cs:296` |
| `POST` | `/session/{s}/window/maximize` | Maximizes the current window. | None | `ContextController.cs:29` |
| `GET` | `/session/{s}/screenshot` | Captures the current window. | None | `ScreenCaptureController.cs:28` |

Notes:

1. The status route reports `ready: false` while a session is attached, because the server allows one session
   (`SessionsController.cs:177-181`). The `sap` member reports whether SAP GUI Scripting is reachable
   (`available`, `connections`, `errorKind`, `message`; `SapScriptingStatusModel.cs:12-30`).
2. Deleting a session only detaches the driver. The SAP GUI windows stay open.
3. The window handle route returns a JSON string with the hexadecimal handle and the title.
4. The switch route accepts a hexadecimal handle, a window name such as `wnd[1]`, or an exact title.

## Locating and reading elements

| Verb | Route | Summary | Request body | Source |
| --- | --- | --- | --- | --- |
| `POST` | `/session/{s}/element` | Finds an element. | Locator | `ElementsController.cs:64` |
| `POST` | `/session/{s}/elements` | Finds elements. | Locator | `ElementsController.cs:131` |
| `POST` | `/session/{s}/element/{e}/element` | Finds an element within an element. | Locator | `ElementsController.cs:98` |
| `POST` | `/session/{s}/element/{e}/elements` | Finds elements within an element. | Locator | `ElementsController.cs:160` |
| `GET` | `/session/{s}/element/{e}/attribute/{name}` | Gets an element attribute. | None | `ElementsController.cs:192` |
| `GET` | `/session/{s}/element/{e}/rect` | Gets the element rectangle. | None | `ElementsController.cs:225` |
| `GET` | `/session/{s}/element/{e}/text` | Gets the element text. | None | `ElementsController.cs:263` |
| `GET` | `/session/{s}/source` | Gets the page source. | None | `DocumentController.cs:29` |
| `GET` | `/session/{s}/dom` | Gets the SAP object model document of the session. | None | `SessionsController.cs:89` |
| `POST` | `/session/{s}/element/dom` | Gets the SAP object model document of a located element. | Locator | `SessionsController.cs:121` |

The locator body has two members, `using` and `value` (`LocationStrategyModel.cs:54-60`). The strategies are
`xpath`, `id`, `tag name` and `css selector` (`LocationStrategyModel.cs:27-46`).

```json
{
    "using": "xpath",
    "value": "//GuiOkCodeField"
}
```

## Interacting with elements

| Verb | Route | Summary | Request body | Source |
| --- | --- | --- | --- | --- |
| `POST` | `/session/{s}/element/{e}/click` | Clicks an element. | None | `ElementsController.cs:293` |
| `POST` | `/session/{s}/element/{e}/value` | Sets the element value. | `{"text": "..."}` | `ElementsController.cs:328` |
| `POST` | `/session/{s}/element/{e}/clear` | Clears a text control. | None | `ElementsController.cs:30` |
| `POST` | `/session/{s}/actions` | Performs key actions as SAP virtual keys. | W3C actions payload | `ActionsController.cs:28` |

The value route replaces the content of the control. It does not type keystrokes. The actions route accepts only
the keys listed in [Capability mapping](capabilities.md#actions-and-keys).

## SAP-only routes

| Verb | Route | Summary | Request body | Source |
| --- | --- | --- | --- | --- |
| `GET` | `/sap/sessions` | Lists the scriptable SAP GUI connections and sessions. | None | `SapController.cs:25` |
| `POST` | `/session/{s}/sap/vkey` | Sends a SAP virtual key. | `{"virtualKey": 0, "window": "wnd[0]"}` | `SapController.cs:123` |
| `GET` | `/session/{s}/sap/statusbar` | Reads the status bar. | None | `SapController.cs:97` |
| `POST` | `/session/{s}/sap/element/at` | Finds the element at a screen point. | `{"x": 110, "y": 205}` | `SapController.cs:53` |
| `POST` | `/session/{s}/sap/element/{e}/call` | Calls an allow-listed method on a control. | `{"method": "...", "arguments": []}` | `SapController.cs:156` |
| `POST` | `/session/{s}/sap/element/{e}/property` | Sets an allow-listed property on a control. | `{"name": "...", "value": "..."}` | `SapController.cs:208` |

The body members come from `SapVirtualKeyModel`, `SapPointModel`, `SapMethodCallModel` and `SapPropertyModel` in
`Sap.DriverServer.Models\SapRequestModels.cs`. The `window` member of the virtual key body is optional. The
allow-lists for methods and properties are in [Capability mapping](capabilities.md#control-methods-and-properties).

## Routes that refuse the request

These routes exist so that a UIA client gets a clear error. They return `unsupported operation`, with
`data.sapErrorKind` set to `notExposedByScripting`.

| Verb | Route | Summary | Source |
| --- | --- | --- | --- |
| `POST` | `/session/{s}/execute/sync` | Executes a script (not supported). | `DocumentController.cs:62` |
| `GET` | `/session/{s}/user32/{**path}` | Refuses User32 read endpoints (not supported). | `User32Controller.cs:23` |
| `POST` | `/session/{s}/user32/{**path}` | Refuses User32 input endpoints (not supported). | `User32Controller.cs:48` |

## Create a session

The new session body is a W3C capabilities object. The `sap:options` member selects the SAP GUI session. Every
member you set must match (logical AND). With no members, the server attaches only when exactly one scriptable
session is open (`Sap.DriverServer.Models\SapOptionsModel.cs:5-12`).

| Member | Type | Default | What it matches |
| --- | --- | --- | --- |
| `sessionPath` | string | none | The scripting id, such as `/app/con[0]/ses[0]`. The `/app/` prefix is optional. |
| `connectionIndex` | integer | none | The zero-based connection index. |
| `sessionIndex` | integer | none | The zero-based session index within the connection. |
| `systemName` | string | none | The SAP system id, such as `DEV`. |
| `client` | string | none | The SAP client, such as `100`. |
| `connectionDescription` | string | none | The SAP Logon description, matched exactly and case-insensitively. |

Source for the members: `Sap.DriverServer.Models\SapOptionsModel.cs:14-48`. Index values must be zero or greater
and a session path must look like `/app/con[n]/ses[n]` (`Sap.DriverServer.Domain\SessionSelector.cs:50-57`). When
more than one session matches, the call fails with `ambiguousSession` and lists the candidates
(`SessionSelector.cs:117-124`).

```json
{
    "capabilities": {
        "alwaysMatch": {
            "sap:options": {
                "systemName": "DEV",
                "client": "100"
            }
        }
    }
}
```

## Check the server

A running server answers `GET /status` with the readiness report. This shape comes from the controller; the values
are illustrative.

```json
{
    "value": {
        "message": "Sessions slots available, can create new session",
        "os": {
            "arch": "X64",
            "name": "Microsoft Windows 10.0.26200",
            "version": "10.0.26200.0"
        },
        "ready": true,
        "sap": {
            "available": true,
            "connections": 1
        },
        "sessions": []
    }
}
```

Source: `SessionsController.cs:175-200`. The default port is `5556` and the `--port` argument changes it
(`Program.cs:30-33`).
