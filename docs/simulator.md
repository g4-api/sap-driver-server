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
| `--recording-disabled` | Sessions refuse `Record = true`, as on a system with `sapgui/user_scripting_disable_recording` |
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
| SAP Easy Access | Logon, or `/n` | Tree shell `cntlIMAGE_CONTAINER/shellcont/shell` | Tree shell, `doubleClickNode`; the **Test lab** folder lists the `ZSIM_*` transactions |
| Form | `/nFORM`, or the tree | Text fields, `ctxtSIM-CARRID`, `chkSIM-ACTIVE`, `radSIM-CLASS_*`, `cmbSIM-CURRENCY`, `tabsTABSTRIP`, `btnOPEN_POPUP` | Click per type, set text, clear, combo keys, tabs, output-only field, F4 help |
| Confirmation popup `wnd[1]` | `btnOPEN_POPUP` | `txtSPOP-TEXTLINE1`, `btnSPOP-OPTION1`, `btnSPOP-OPTION2` | Window switch, blocked `wnd[0]`, popup screenshot |
| Grid | `/nGRID`, or the tree | Grid view shell `cntlGRID1/shellcont/shell` with 12 synthetic flights | Inner objects, `GetCellValue`, `SetCurrentCell`, cell framing |
| Test lab | `/nZSIM_*`, or the tree | See [Test lab](#test-lab) | One area of SAP GUI automation pitfalls per transaction |

The main window takes these virtual keys; keyboard shortcuts and the toolbar buttons send the same keys.

| Key | Name | Effect |
| ---: | --- | --- |
| 0 | Enter | Logs on, runs the command field, or checks the screen's entries when the command field is empty |
| 2 | Pick (F2) | Opens the secondary list of the line under the cursor in `ZSIM_LIST` |
| 3 | Back (F3) | Returns to SAP Easy Access; from there, logs off; in a secondary list, returns to the basic list |
| 4 | Input help (F4) | Opens the hit list of the focused field when it has an input help |
| 11 | Save (Ctrl+S) | Reports a simulated save on the form |
| 12 | Cancel (F12) | Returns to SAP Easy Access; on a cancelable popup, cancels it |
| 15 | Exit (Shift+F3) | Same as Back |
| 80 to 83 | Paging | First, previous, next, and last page of `ZSIM_TABLE` and `ZSIM_LIST` |

Messages use the message class `ZSIM`, so a test can tell simulator messages from SAP messages. Success (`S`),
warning (`W`), and error (`E`) messages appear in the status bar; information (`I`) and termination (`A`) messages
appear in a popup. A message shown in a popup stays the status bar's message, with `MessageAsPopup` set.

## Test lab

The test lab transactions each collect the controls and automation pitfalls of one area. Start them with their
transaction code, such as `/nZSIM_TABLE`, or from the **Test lab** folder in SAP Easy Access. Their data is synthetic
and deterministic, and it resets whenever the transaction starts again.

The behavior follows the SAP GUI Scripting documentation and scripting experience; it has not been compared with a
live SAP system. Where the documentation leaves a detail open, such as the exact text of an identifier or a format,
the simulator makes a choice that real SAP GUI may not share.

| Transaction | Area | Main controls |
| --- | --- | --- |
| `ZSIM_FIELDS` | Field attributes | `ctxtZSIM_FIELDS-BUKRS`, `txtZSIM_FIELDS-AMOUNT`, `txtZSIM_FIELDS-BELNR` |
| `ZSIM_MESSAGES` | Message types and popups | `btnMSG_S` to `btnMSG_A`, `chkMSG_AS_POPUP`, `btnPOPUP_CONFIRM` |
| `ZSIM_TABS` | Tab strips and subscreens | `tabsTS_MAIN`, `tabsTS_INNER` |
| `ZSIM_TABLE` | Table control | `tblSAPMZSIM_TABLETC_ITEMS` |
| `ZSIM_ALV` | ALV grid | `cntlALV_CONTAINER/shellcont/shell` |
| `ZSIM_TREES` | Tree controls | `cntlTREE_SIMPLE`, `cntlTREE_LIST`, `cntlTREE_COLUMN` |
| `ZSIM_SHELLS` | Shell controls, boxes, scroll containers | `cntlTOOLBAR`, `cntlSPLITTER`, `boxZSIM_SHELLS-ADDRESS`, `ssubSCROLL_AREA:SAPMZSIM_SHELLS:0200` |
| `ZSIM_LIST` | Classic lists | `lbl[column,line]` labels |
| `ZSIM_CHALLENGE` | Timing, state, and identity traps | `btnCHALLENGE_*`, `subCHALLENGE_DETAIL:*`, `ssubCHALLENGE_SCROLL:*` |

### ZSIM_FIELDS

1. `ctxtZSIM_FIELDS-BUKRS` is a required field (`Required = true`); Enter with it empty reports error `050`.
2. `txtZSIM_FIELDS-AMOUNT` is numeric (`Numerical = true`) and right-aligned; a text value reports error `051`.
3. `txtZSIM_FIELDS-SHORT` has `MaxLength` 4; a longer text set by a script is cut to four characters.
4. `txtZSIM_FIELDS-BELNR` is a padded output field: `Text` reads `4711`, while `DisplayedText` keeps the blanks.
5. `txtZSIM_FIELDS-ERNAM` is output only and `txtZSIM_FIELDS-MONAT` is disabled; both report `Changeable = false`.
6. `setFocus` on `ctxtZSIM_FIELDS-CARRID` or `ctxtZSIM_FIELDS-BUKRS`, then `sendVKey 4`, opens the hit list in
   `wnd[1]`. Select a row of `usr/cntlRESULTLIST/shellcont/shell` and press Enter to copy its key into the field.
7. F4 on a field without an input help reports warning `007`.

### ZSIM_MESSAGES

1. The success, warning, and error buttons report in the status bar, or in a popup when `chkMSG_AS_POPUP` is
   selected.
2. The information button always opens a popup; its text is `wnd[1]/usr/txtMESSTXT1`, and `wnd[1]/tbar[0]/btn[0]`
   continues. A message popup cannot be cancelled with F12.
3. The termination button opens a popup and ends the transaction when it closes; SAP Easy Access then shows the
   message.

### ZSIM_TABS

1. Each tab of `tabsTS_MAIN` fills the subscreen area `SUB_MAIN` with its own subscreen, so a field's identifier
   includes the tab's subscreen, as in `tabpT\01/ssubSUB_MAIN:SAPMZSIM_TABS:0110/txtZSIM_TABS-NOTE`.
2. The note field exists on two tabs, under two identifiers.
3. Only the selected tab's content exists; an identifier recorded on another tab fails until that tab is selected.
4. The third tab nests the tab strip `tabsTS_INNER`.

### ZSIM_TABLE

1. The table control shows 12 lines over 200 rows: `RowCount` is 200 and `VisibleRowCount` is 12.
2. Cells exist only for the visible lines, as `txtITAB-POSNR[column,line]`; the line counts from the first visible
   row, so `[0,12]` does not exist.
3. `verticalScrollbar.position` and the paging keys 80 to 83 scroll the table; the same cell identifier then shows
   another row. Edits are kept when the table scrolls.
4. Lines past the last row are empty and not changeable.
5. `getAbsoluteRow(n).selected` selects a data row, independent of scrolling.

### ZSIM_ALV

1. The grid holds 500 rows but loads only rows that have been in view: `getCellValue` returns an empty string for
   a row that was never shown, until `firstVisibleRow` scrolls to it.
2. Toolbar buttons are pressed by function code. `&MB_EXPORT` is a menu button: press it with
   `pressToolbarContextButton`, then choose `&XXL` or `&PC` with `selectContextMenuItem`.
3. `contextMenu` opens the cell menu (`&COPY`, `&DETAIL`); a menu stays open until an item is selected.
4. `&SORT_ASC` and `&SORT_DSC` reorder the rows, so a row index names another flight afterwards.
5. `selectedRows` accepts ranges such as `2-4,7`.
6. The flight number column is a hotspot: `clickCurrentCell` raises `&IC1`. A double click opens a detail popup.
7. Export and copy only report what they would do; they write no files and leave the clipboard unchanged.

### ZSIM_TREES

1. The same hierarchy appears as a simple tree (type 0), a list tree (type 1), and a column tree (type 2).
2. Node keys are padded to ten characters, as in `"         1"`; the key `"1"` finds nothing.
3. Folders load their children on the first expansion; before that, a child key is unknown to every member.
4. List and column trees hold their texts in items: `getItemText(key, "PRICE")` in the column tree, and the item
   names `1` to `3` in the list tree.
5. `doubleClickItem` reports the item and sets `selectedItemNode`.

### ZSIM_SHELLS

1. The toolbar control has a plain, a disabled, a check, a button-and-menu, and a menu button. Disabled and
   menu-only buttons refuse `pressButton`.
2. The splitter is a shell that contains shells: the text edit is `cntlSPLITTER/shellcont/shell/shellcont[0]/shell`
   and the picture is `cntlSPLITTER/shellcont/shell/shellcont[1]/shell`.
3. The box `boxZSIM_SHELLS-ADDRESS` only draws a frame; the fields inside it on screen are its siblings.
4. The scroll container `ssubSCROLL_AREA:SAPMZSIM_SHELLS:0200` holds 25 fields; most of them report coordinates
   below its visible area until `verticalScrollbar.position` scrolls them into view.

### ZSIM_LIST

1. The list has no named fields; each output field is a label named by its character position on the page, as in
   `usr/lbl[10,5]`, with `IsListElement`, `CharLeft`, and `CharTop`.
2. The paging keys and `usr/verticalScrollbar.position` change what each label identifier shows.
3. `setFocus` on a data label, then `sendVKey 2`, opens a secondary list; Back returns to the same page.

### ZSIM_CHALLENGE

1. `btnCHALLENGE_DELAYED` appears 2 seconds after the screen opens; a lookup right away finds nothing.
2. `btnCHALLENGE_BUSY` keeps the session `Busy` for 1.5 seconds and refuses virtual keys meanwhile. The driver waits
   for the session to be idle before it sends a key.
3. `btnCHALLENGE_POST` asks for confirmation on some presses only, in a fixed irregular pattern; check for `wnd[1]`
   after every press.
4. Two buttons share the text `Select`, so a locator by text finds both.
5. The mode radio buttons replace the subscreen `subCHALLENGE_DETAIL`, so the value field is
   `txtCHALLENGE-A_VALUE` in mode A and `ctxtCHALLENGE-B_VALUE` in mode B.
6. Enter checks the date before the quantity: an invalid date reports error `113` and locks the other fields until
   it is fixed, and a quantity above 100 reports warning `115` until a second Enter confirms it.
7. `btnCHALLENGE_HIDDEN` lies below the visible part of a scroll container. Scripting presses it; a click at its
   coordinates would miss.

### Driver coverage

The end-to-end tests drive the test lab through the driver. Shell methods and properties go through the allow-listed
`sap/element/{e}/call` and `sap/element/{e}/property` routes; see
[Control methods and properties](capabilities.md#control-methods-and-properties). Only the session's `Busy` flag is
read through the inspector's local member invoker, because the session is not an element.

## Recording events

Sessions raise `GuiSession.Change` through a COM connection point while `Record` is `true`, so the G4 SAP recorder
can be tested without SAP GUI. Subscribers are called through `IDispatch.Invoke` with the arguments session,
component, and command array; each command is an array of a kind (`SP` sets a property, `M` calls a method), a member
name, and arguments.

| User or script action | Reported command |
| --- | --- |
| Button press | `M press` on the button |
| Radio button selection | `M select` on the radio button |
| Check box click | `SP selected` with the new state |
| Field text change | `SP text` with the field's scripting text, reported before the submitting action |
| Virtual key, such as Enter | `M sendVKey` with the key, on the window |

1. Field text is reported when the screen is submitted, not per keystroke, and password fields report empty text.
2. Tab selections, shell actions, and menu selections are not reported.
3. The dispatch identifier (`1`), the event interface identifier, and the command codes are the simulator's model of
   SAP GUI's event; they have not been compared with a real SAP GUI.

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
indexed session selection, both inspector sources, the published driver, and every test lab transaction.

## Limitations

1. Only the members the driver and the inspector use are implemented; other members report "not exposed" as SAP GUI
   does for absent members.
2. Server round trips are not modeled, except the busy period of `ZSIM_CHALLENGE`; elsewhere `Busy` is `false`.
3. Menus beyond the simulator menu and the test lab's toolbar and context menus are not simulated.
4. Overlay and canvas alignment were checked on a display at 100% scaling only.
5. The test lab follows documented and commonly observed SAP GUI behavior; it was not compared with a live SAP system.
