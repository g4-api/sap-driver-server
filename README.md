# SAP Driver Server

SAP Driver Server is a W3C WebDriver server for SAP GUI for Windows. It mirrors the G4
[UIA driver server](https://github.com/g4-api/uia-driver) protocol, routes, response envelopes, and error format, and
drives SAP GUI through the SAP GUI Scripting API over Windows COM instead of Microsoft UI Automation (UIA).

The server attaches to SAP GUI sessions that are already open. It never opens connections, logs on, or handles
credentials.

## Contents

1. [Prerequisites](#prerequisites)
2. [Download a release](#download-a-release)
3. [Build and run](#build-and-run)
4. [Connect from G4 or Selenium](#connect-from-g4-or-selenium)
5. [Session selection](#session-selection)
6. [Locating controls](#locating-controls)
7. [Capabilities at a glance](#capabilities-at-a-glance)
8. [Differences from the UIA driver server](#differences-from-the-uia-driver-server)
9. [Configuration](#configuration)
10. [Architecture](#architecture)
11. [SAP GUI Inspector](#sap-gui-inspector)
12. [SAP GUI simulator](#sap-gui-simulator)
13. [Tests](#tests)
14. [Further reading](#further-reading)

## Prerequisites

The driver uses SAP GUI Scripting exactly as SAP ships it. Every SAP-side setting below is configured by the SAP
administrator or the user; the driver does not change any of them. The driver itself needs no SAP installation,
registration, or SDK: it finds SAP Logon in the Windows running object table at run time.

1. Run SAP GUI for Windows, with the **SAP GUI Scripting** component, on the machine where the driver runs.
2. Enable scripting on the client: SAP GUI Options > Accessibility & Scripting > Scripting > **Enable scripting**.
3. Optionally clear the two "Notify when a script…" options there, so SAP GUI does not show a confirmation dialog.
4. Ask the SAP administrator to allow scripting on the application server with the profile parameter
   `sapgui/user_scripting = TRUE`; check it in transaction `RZ11`.
5. Make sure the user has authorization object `S_SCR` when the system restricts scripting per user
   (`sapgui/user_scripting_per_user = TRUE`).
6. Install the .NET 10 SDK to build. Builds from `scripts\publish.ps1` are self-contained and need no .NET runtime;
   the GitHub release zips are framework-dependent and need the .NET 10 runtime.
7. Run the driver server as the same Windows user, in the same desktop session and at the same integrity level
   (elevated or not) as SAP Logon. Windows shows a process only the running object table entries of its own user
   and integrity level, so a mismatch reports `sapGuiNotRunning` or `accessDenied`.
8. Use either process bitness. The driver reads SAP Logon's `SAPGUI` entry from the running object table directly,
   not through the `SapROTWr` component, so a 64-bit driver works with 32-bit and 64-bit SAP GUI.

## Download a release

Every push to `main` that changes more than documentation runs the release pipeline: it runs the unit tests,
publishes the driver and the inspector, and creates a GitHub release tagged `v<yyyy.MM.dd.run>`. A preview release,
tagged `v<yyyy.MM.dd.run>-preview` and marked as a pre-release, is built on demand from the Actions tab.

1. Open the [releases page](https://github.com/g4-api/sap-driver-server/releases).
2. Download `Sap.DriverServer.<version>-win-x64.zip` for the driver server and
   `Sap.DriverServer.Inspector.<version>-win-x64.zip` for the SAP GUI Inspector.
3. Install the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) on the target machine; the zips
   are framework-dependent. The inspector needs the .NET Desktop Runtime.
4. Extract a zip and run `Sap.DriverServer.exe` or `Sap.DriverServer.Inspector.exe`. The driver console shows the
   release version in its logo.

## Build and run

The solution builds with the plain `dotnet` CLI. SAP GUI is not required at build time, because the driver binds to
SAP GUI Scripting at run time through `IDispatch`.

1. Build and test the solution.

    ```powershell
    dotnet build src\Sap.sln
    dotnet test src\Sap.DriverServer.Domain.UnitTests
    ```

2. Run the server on the default port `5556`, next to the UIA server on `5555`.

    ```powershell
    dotnet run --project src\Sap.DriverServer -- --port 5556
    ```

3. Publish the driver and the inspector as self-contained, single-file x64 executables, and the simulator as an x86
   one, in `artifacts\`.

    ```powershell
    .\scripts\publish.ps1
    ```

4. Open `http://localhost:5556/swagger` for the OpenAPI UI, or `http://localhost:5556/status` for a readiness report
   that includes whether SAP GUI Scripting is reachable.

## Connect from G4 or Selenium

Every route is served under both `/` and `/wd/hub/`, so any W3C client works. G4 connects through `RemoteWebDriver`
with the server address and the `sap:options` capability.

1. Open SAP Logon, open a connection, and log on.
2. Optionally list the scriptable sessions with `GET /sap/sessions` to pick selection values.
3. Create a session.

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

4. Locate controls and interact with them through the standard element endpoints.
5. Delete the session when done. Deleting only detaches the driver; SAP GUI windows stay open.

A C# client example with the G4 client library:

```csharp
var options = new Dictionary<string, object>
{
    ["sap:options"] = new Dictionary<string, object> { ["sessionPath"] = "/app/con[0]/ses[0]" }
};
var session = new SessionModel { Capabilities = new CapabilitiesModel { AlwaysMatch = options } };
var driver = new RemoteWebDriver(new Uri("http://localhost:5556"), session);

driver.FindElement(By.Xpath("//GuiOkCodeField")).SendKeys("/nSE16");
driver.FindElement(By.Xpath("//GuiButton[@name='btn[0]']")).Click();
```

## Session selection

The `sap:options` capability selects exactly one open SAP GUI session. Every supplied criterion must match.

| Member | Type | Matches |
| --- | --- | --- |
| `sessionPath` | string | Scripting ID such as `/app/con[0]/ses[1]`; the `/app/` prefix is optional |
| `connectionIndex` | integer | The `con[n]` index of the SAP Logon connection |
| `sessionIndex` | integer | The `ses[n]` index within the connection |
| `connectionDescription` | string | The SAP Logon entry description, case-insensitive |
| `systemName` | string | The system ID (SID), case-insensitive |
| `client` | string | The SAP client number |

The selection rules are deterministic.

1. With no criteria, the server attaches only when exactly one scriptable session is open.
2. When several sessions match, creation fails with `session not created` and `sapErrorKind: ambiguousSession`. The
   error data lists the candidates by ID, system, client, and connection description.
3. When nothing matches, creation fails with `noMatchingSession` and lists the available sessions.
4. Sessions of connections whose server disabled scripting are never candidates. If such a connection matches the
   criteria, the error is `scriptingDisabledByServer`.
5. The driver attaches to the SAP GUI instance registered in the Windows running object table as `SAPGUI`. That is
   the first SAP Logon process; other SAP GUI processes are not reachable through scripting.

The new session response adds a `sap:session` capability with the attached session's ID, system, client, and
connection description. User names, transactions, and screen contents are never collected or logged.

## Locating controls

Element references map to SAP scripting IDs and are resolved again through `findById` on every call. A control that
disappeared, for example after a screen change, returns `stale element reference`.

| Strategy | Example | Evaluation |
| --- | --- | --- |
| `xpath` | `//GuiButton[@tooltip='Save (Ctrl+S)']` | XPath 1.0 over the SAP object model document |
| `id` | `wnd[0]/usr/txtRSYST-BNAME` | `findById`, absolute (`/app/…`) or relative to the session or parent |
| `tag name` | `GuiTextField` | Every descendant of that SAP `Type` |
| `css selector` | `#wnd[0]/tbar[0]/btn[3]`, `[name='RSYST-BNAME']` | Simple forms only: `#id`, `Type`, `[attr='v']`, `Type[attr='v']` |

The object model document (`GET /session/{id}/source` or `/dom`) uses SAP types as element names and the attributes
`id`, `type`, `name`, `text`, `tooltip`, `changeable`, and `subType` for shells. Password text is never read.

XPath is resolved in one of three ways, and all of them return the same matches in document order.

1. Simple paths are resolved directly on the live SAP tree, much like the UIA driver's XPath parser. This covers
   steps joined by `/` or `//`, a SAP type or `*` as the node test, and `[@attribute='value']` or `[n]`
   predicates. Only the objects on the path, and only the properties the predicates need, are read.
2. `//Type[@id='/app/…']` resolves through `findById` without walking the tree.
3. Every other expression, for example with `contains()`, `and`, `or`, or other axes, is evaluated as full XPath
   1.0 over the object model document. This reads the whole session tree, so prefer simple paths or the `id`
   strategy on dense screens.

## Capabilities at a glance

| Area | Status | Notes |
| --- | --- | --- |
| Sessions, status, delete | Implemented | Attach and detach only |
| Find element(s), nested find | Implemented | `xpath`, `id`, `tag name`, simple `css selector` |
| Click | Implemented per control type | Buttons press, tabs and radios select, check boxes toggle, fields focus |
| Send keys, clear | Implemented for text controls | Replaces the text; combo boxes select by key |
| Text, attribute, rect | Implemented | Whitelisted SAP properties; password text is never returned |
| Source and DOM | Implemented | SAP object model XML |
| Window handle(s), switch, maximize | Implemented | SAP frame windows `wnd[n]` |
| Screenshot | Implemented | SAP window `HardCopy` as PNG |
| Actions | Limited | Key actions that map to SAP virtual keys only |
| SAP-only endpoints | Implemented | `/sap/sessions`, `sap/vkey`, `sap/statusbar`, `sap/element/at` |
| Execute script | Not supported | Returns `unsupported operation` |
| User32 endpoints, `/scancodes` | Not supported | Raw Windows input has no scripting equivalent |
| Shell internals (grid cells, tree nodes) | Not supported | Visible as one `GuiShell` element |
| Events and recording | Not supported | The UIA server has no event contract to mirror |

See [Capability mapping](docs/capabilities.md) for every route and [Troubleshooting](docs/troubleshooting.md) for
error kinds.

## Differences from the UIA driver server

1. Sessions attach to existing SAP GUI sessions selected by `sap:options`; `uia:options` (`app`, `arguments`,
   `mount`, impersonation) is ignored, and no process is started or killed.
2. Send keys sets the control text instead of typing keystrokes, so it replaces the content rather than appending.
3. Screenshots capture the SAP window, not the whole desktop.
4. Window handles are SAP frame window handles in uppercase hexadecimal; switching also accepts `wnd[n]` or the exact
   window title.
5. `execute/sync`, every `user32/*` route, pointer and wheel actions, and character key actions return a structured
   `unsupported operation` error with `data.sapErrorKind: notExposedByScripting`. The UIA OCR and coordinate locators
   are not available.
6. Errors caused by SAP or COM carry `data.sapErrorKind` and no stack trace; COM error texts stay in the server log.
7. `/scancodes` and the Grid 3 `register` command are not provided.

## Configuration

`appsettings.json` contains a `SapDriver` section.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `BusyWaitMilliseconds` | 10000 | How long a request waits while the SAP session is busy before `sessionBusy` |
| `ComCallTimeoutMilliseconds` | 60000 | How long a request waits for its COM operation, including queue time, before `timeout` |

The listening port comes from the `--port` argument and defaults to `5556`.

### Environment settings

The `Environment` section of `appsettings.json` holds the switches and paths the tests read. Each key keeps its
environment variable name, so one name works in three places. A later place wins:

1. The `Environment` section of `appsettings.json` in the application folder.
2. A `.env` file of `KEY=VALUE` lines next to `appsettings.json`.
3. The shell, so a container or a CI job always gets the values it sets.

| Key | Default | Meaning |
| --- | --- | --- |
| `G4_SAP_INTEGRATION` | `false` | Runs the read-only live SAP GUI tests |
| `G4_SAP_SIMULATOR` | `false` | Runs the SAP GUI simulator end-to-end tests |
| `G4_SAP_SIMULATOR_PATH` | Empty | Simulator executable; empty uses the build output |
| `G4_SAP_DRIVER_PATH` | Empty | Published driver for the published-driver test; empty uses `artifacts\sap-driver-server` |

Switches take `true` or `false`; `1` and `0` also work. To keep local values out of source control, copy
`.env.example` to `.env` at the repository root. The build copies it next to `appsettings.json`, including the test
output, and publishing never copies it. After you delete `.env`, rebuild cleanly, because the copies in `bin` stay.
`Sap.DriverServer.Settings` loads these values as `AppSettings.Environment`.

## Architecture

| Project | Target | Role |
| --- | --- | --- |
| `Sap.DriverServer` | `net10.0-windows` | ASP.NET Core host, W3C controllers, error middleware |
| `Sap.DriverServer.Domain` | `net10.0` | Repositories, session selection, locators, control mapping, COM-free abstractions |
| `Sap.DriverServer.Models` | `net10.0` | W3C contracts ported from the UIA server, SAP options and discovery models |
| `Sap.DriverServer.Scripting` | `net10.0-windows` | STA dispatcher, running object table reader, and late-bound SAP GUI Scripting engine |
| `Sap.DriverServer.Settings` | `net10.0` | Layered configuration and the typed `Environment` settings |
| `Sap.DriverServer.Inspector` | `net10.0-windows` | WPF inspector for discovery, live inspection, locators, and ad-hoc actions |
| `Sap.DriverServer.Simulator` | `net10.0-windows`, x86 | WPF test double for SAP Logon that publishes `SAPGUI` and SAP-like scripting objects |
| `Sap.DriverServer.Domain.UnitTests` | `net10.0-windows` | MSTest unit, contract, and opt-in integration and end-to-end tests |

All COM calls run on one dedicated single-threaded apartment (STA) thread through `StaComDispatcher`, so concurrent
HTTP requests never drive SAP GUI at the same time. The dispatcher thread owns every COM reference. Objects created
during an operation are released when the operation ends, and the cached `GuiApplication` object is released at
shutdown or after SAP GUI disconnects. Sessions store SAP IDs only, never COM references.

## SAP GUI Inspector

`Sap.DriverServer.Inspector` is a WPF tool in the style of Accessibility Insights Live Inspect. It needs no driver:
hover over a SAP GUI control on the same machine to frame it and see its scripting ID, properties, and verified
driver locators. Press Shift+F5 to pause, then try the driver's interactions or call any scripting member on the
selection. **Create session** connects it to a driver server, also on another machine, like Appium Inspector, and
shows a screenshot canvas you can hover. Start it with `dotnet run --project src\Sap.DriverServer.Inspector`; see
[SAP GUI Inspector](docs/inspector.md) for details.

## SAP GUI simulator

`Sap.DriverServer.Simulator` is a 32-bit test double for SAP Logon. It registers `SAPGUI` in the running object table
and exposes SAP-named scripting objects over real windows: a logon screen, SAP Easy Access, a form, a popup, and a
grid. The driver and the inspector attach to it with their production code, so you can try them, and test them end to
end, on a machine without SAP GUI. It cannot prove that SAP GUI behaves like the simulator; see
[SAP GUI simulator](docs/simulator.md) for what it covers.

## Tests

1. Run the unit and contract tests, which use an in-memory SAP object tree and need no SAP GUI.

    ```powershell
    dotnet test src\Sap.DriverServer.Domain.UnitTests --filter TestCategory=UnitTest
    ```

2. Run the read-only live tests against an open SAP GUI session. They only read the session tree and status bar,
   and report Inconclusive when SAP GUI or scripting is unavailable. Enable them with `G4_SAP_INTEGRATION=true` in
   `.env`, or in the shell as shown; see [Environment settings](#environment-settings).

    ```powershell
    $env:G4_SAP_INTEGRATION = "true"
    dotnet test src\Sap.DriverServer.Domain.UnitTests --filter TestCategory=SapIntegration
    ```

3. Run the end-to-end tests against the SAP GUI simulator. Each test starts the simulator, drives it through the
   driver or the inspector, and closes it. They report Inconclusive when SAP Logon or another simulator is running.
   Enable them with `G4_SAP_SIMULATOR=true` in `.env`, or in the shell as shown.

    ```powershell
    $env:G4_SAP_SIMULATOR = "true"
    dotnet test src\Sap.DriverServer.Domain.UnitTests --filter TestCategory=SapSimulator
    ```

## Further reading

1. [Capability mapping](docs/capabilities.md)
2. [Troubleshooting](docs/troubleshooting.md)
3. [SAP GUI Inspector](docs/inspector.md)
4. [SAP GUI simulator](docs/simulator.md)
5. [SAP GUI Scripting API documentation](https://help.sap.com/docs/sap_gui_for_windows)
6. [W3C WebDriver specification](https://www.w3.org/TR/webdriver/)
