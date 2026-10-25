# SAP GUI Simulator

The SAP GUI simulator (`Sap.DriverServer.Simulator`) is a test double for SAP Logon. It lets you run the driver
server and the SAP GUI Inspector end to end on a machine without SAP GUI, through the same Windows running object
table entry and COM calls they use with SAP GUI.

The simulator is not SAP software and contains no SAP code or data. Every window title ends in
"SAP GUI Simulator (test double)", and the connection description starts with `SIM`.

## What it proves

The simulator runs as a separate 32-bit process, like SAP GUI for Windows, and publishes a `SAPGUI` entry in the
running object table. The 64-bit driver and inspector attach to it with their production code, unchanged.

| Area | Proven with the simulator | Still needs a real SAP GUI |
| --- | --- | --- |
| Attach | Direct running object table attach from 64-bit to 32-bit, with no SAP registration | Attach to SAP Logon's own entry |
| COM calls | Cross-process, cross-bitness `IDispatch` calls through the STA dispatcher | SAP GUI's member set and error codes |
| Driver | Sessions, selection errors, locators, XPath, actions, virtual keys, windows, screenshots, element-at | Behavior on real dynpro screens |
| Inspector | Local hover with window mapping, overlay and canvas alignment, locators, driver sessions | Alignment at display scaling other than the one tested |
| Packaging | The published single-file driver and simulator executables | Not applicable |

The simulator follows the SAP GUI Scripting API as documented, but it implements only the members the driver and the
inspector use. Where SAP's documentation leaves a detail open, the simulator makes a choice, and real SAP GUI may differ.

1. `findByPosition` returns the absolute control identifier and an inner-object description, such as
   `Cell(2,PRICE)` for a grid cell.
2. Screen coordinates are physical pixels, and a window's geometry is its client area, which `HardCopy` captures.
3. Refused requests fail with the generic COM error `E_FAIL` and a message.
4. Virtual key descriptions use the pattern `F3`, `Shift+F3`, `Ctrl+F3`, and `Ctrl+Shift+F3`.

## Run

1. Build the solution, or publish the simulator as a single 32-bit executable.

    ```powershell
    dotnet build src\Sap.sln
    .\scripts\publish.ps1
    ```

2. Close SAP Logon if it runs. The simulator refuses to start while another process owns `SAPGUI`.
3. Start the simulator. Each session opens its own main window on the logon screen.

    ```powershell
    .\artifacts\sap-gui-simulator\Sap.DriverServer.Simulator.exe --sessions 2
    ```

4. Log on with any user and password. The password is checked for presence only, cleared at once, and never stored.
5. Close every session window to end the simulator; it removes its `SAPGUI` entry on exit.

| Option | Effect |
| --- | --- |
| `--sessions <n>` | Opens 1 to 4 sessions on the connection; the default is 1 |
| `--scripting-disabled` | `GetScriptingEngine` returns nothing, as when SAP GUI Scripting is disabled on the client |
| `--disabled-by-server` | The connection reports `DisabledByServer` and hides its sessions |
| `--quiet` | Reports startup failures only through the exit code, without message boxes |

| Exit code | Meaning |
| ---: | --- |
| 0 | The last session window closed |
| 2 | An argument is unknown or out of range |
| 3 | A live process already registered `SAPGUI`, such as SAP Logon or another simulator |
| 4 | COM refused the registration |

## Screens

Every screen uses SAP GUI's identifier prefixes, so the identifiers select the same scripting types as in SAP GUI.
The main window `wnd[0]` holds the menu bar `mbar`, the system toolbar `tbar[0]` with the command field `okcd`, the
title bar `titl`, the application toolbar `tbar[1]`, the user area `usr`, and the status bar `sbar`.

| Screen | Start | Controls in `usr` | Exercises |
| --- | --- | --- | --- |
| Logon | Session start, or log off | `txtRSYST-MANDT`, `txtRSYST-BNAME`, `pwdRSYST-BCODE`, `txtRSYST-LANGU` | Send keys, the password rule, Enter |
| SAP Easy Access | Logon, or `/n` | Tree shell `cntlIMAGE_CONTAINER/shellcont/shell` | Tree shell, `doubleClickNode` |
| Form | `/nFORM`, or the tree | Text fields, `ctxtSIM-CARRID`, `chkSIM-ACTIVE`, `radSIM-CLASS_*`, `cmbSIM-CURRENCY`, `tabsTABSTRIP`, `btnOPEN_POPUP` | Click per type, set text, clear, combo keys, tabs, output-only field |
| Popup `wnd[1]` | `btnOPEN_POPUP` | `txtSPOP-TEXTLINE1`, `btnSPOP-OPTION1`, `btnSPOP-OPTION2` | Window switch, blocked `wnd[0]`, popup screenshot |
| Grid | `/nGRID`, or the tree | Grid view shell `cntlGRID1/shellcont/shell` with 12 synthetic flights | Inner objects, `GetCellValue`, `SetCurrentCell`, cell framing |

The main window takes these virtual keys; keyboard shortcuts and the toolbar buttons send the same keys.

| Key | Name | Effect |
| ---: | --- | --- |
| 0 | Enter | Logs on, or runs the command field |
| 3 | Back (F3) | Returns to SAP Easy Access; from there, logs off |
| 11 | Save (Ctrl+S) | Reports a simulated save on the form |
| 12 | Cancel (F12) | Returns to SAP Easy Access; on the popup, cancels it |
| 15 | Exit (Shift+F3) | Same as Back |

Messages in the status bar use the message class `ZSIM` and the types `S`, `W`, and `E`, so a test can tell
simulator messages from SAP messages.

## Walk through the driver and the inspector

1. Start the simulator, then the driver server.

    ```powershell
    .\artifacts\sap-gui-simulator\Sap.DriverServer.Simulator.exe
    .\artifacts\sap-driver-server\Sap.DriverServer.exe --port 5556
    ```

2. Open `http://localhost:5556/status`; `sap.available` is `true` with one connection.
3. Create a session with `{"sap:options": {"systemName": "SIM"}}` and log on through the element endpoints, as in
   [Connect from G4 or Selenium](../README.md#connect-from-g4-or-selenium).
4. Start the inspector. It attaches to the simulator on its own; hover over a simulator control to frame it.
5. In the inspector, use **Create session** with the same options to inspect through the driver and its screen canvas.

## End-to-end tests

The end-to-end tests start the simulator for each test, drive it through an in-process driver server with the
production COM engine, and close it again. They never touch a real SAP GUI: when another process owns `SAPGUI`,
they report Inconclusive instead of attaching to it.

1. Build the solution; the test project builds the simulator.
2. Enable the tests with `G4_SAP_SIMULATOR=true` in a repository-root `.env`, copied from `.env.example`, and
   rebuild so the build copies it next to the tests.
3. Run the tests.

    ```powershell
    dotnet test src\Sap.DriverServer.Domain.UnitTests --filter TestCategory=SapSimulator
    ```

4. Optionally test published executables. `G4_SAP_SIMULATOR_PATH` selects the simulator executable, and
   `G4_SAP_DRIVER_PATH` selects the driver executable for the published-driver test, which otherwise uses
   `artifacts\sap-driver-server` and reports Inconclusive when it is missing.

A shell variable wins over `.env`, and `.env` wins over the `Environment` section of `appsettings.json`. For example,
`$env:G4_SAP_SIMULATOR = "false"` skips the tests for one shell even when `.env` enables them. See
[Environment settings](../README.md#environment-settings) for every key.

The tests cover status, logon, OK-code navigation, form interactions, output-only fields, the popup window,
element-at on a button and a grid cell, page source and XPath, both disabled-scripting scenarios, ambiguous and
indexed session selection, both inspector sources, and the published driver.

## Limitations

1. Only the members the driver and the inspector use are implemented; other members report "not exposed" as SAP GUI
   does for absent members.
2. The simulator does not model server round trips, so `Busy` is always `false`.
3. Input helps (F4), menus beyond the simulator menu, and message popups are not simulated.
4. Overlay and canvas alignment were checked on a display at 100% scaling only.
