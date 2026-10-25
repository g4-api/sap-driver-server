# SAP GUI Inspector

The SAP GUI Inspector (`Sap.DriverServer.Inspector`) is a WPF tool for discovering and inspecting SAP GUI for Windows
controls, in the style of Accessibility Insights for Windows Live Inspect. It uses the same SAP GUI Scripting layer
as the driver server, so the identifiers, locators, and interactions it shows behave exactly as WebDriver calls do.

The inspector does not need the driver server. It inspects SAP GUI on the same machine directly over COM, and it can
also connect to a driver server through **Create session**, like Appium Inspector, to inspect SAP GUI on another
machine.

## Prerequisites

1. The inspector needs no .NET installation, SAP installation, or SAP registration. The published build is one
   self-contained executable that finds SAP Logon in the Windows running object table, with either SAP GUI bitness.
2. Local inspection needs SAP Logon, with SAP GUI Scripting enabled, on the same machine. Run the inspector as the
   same Windows user and at the same integrity level (elevated or not) as SAP Logon. See the
   [README prerequisites](../README.md#prerequisites) for the SAP-side settings.
3. Driver sessions need a running SAP driver server that the inspector can reach over HTTP.

## Run

1. Start the inspector, either the published executable or from source.

    ```powershell
    .\artifacts\sap-inspector\Sap.DriverServer.Inspector.exe
    dotnet run --project src\Sap.DriverServer.Inspector
    ```

2. On a machine with SAP Logon, log on and hover over any SAP GUI control. The control is framed on screen and its
   details appear in the inspector.
3. On a machine without SAP Logon, the inspector shows a waiting hint instead of an error. It checks for SAP Logon
   every 3 seconds and starts local inspection on its own; meanwhile, use **Create session** to inspect through a
   driver.

## Window layout

| Area | Content |
| --- | --- |
| App bar | The G4 mark, the Highlighter and Follow focus switches, and Pause or Resume (Shift+F5) |
| Inspect pane | The source selector, Create session, Disconnect, and the tree with Refresh and the navigation buttons |
| Screen canvas | Driver sessions only: a screenshot of the current SAP window with the highlights drawn over it |
| Details pane | The type and name, the full scripting ID with Copy id, and four tabs |
| Status line | The attached source, the last action, and any SAP error with its kind |

The details pane shows one group at a time, so a long inspection stays readable:

1. **Properties:** the filter, the Show all switch, and the property grid.
2. **Locators:** the generated locators with their verification, and the locator tester, which highlights every match.
3. **Actions:** the driver's quick actions for the control type and the text they use.
4. **Advanced:** virtual keys, Maximize, and Read status bar for the window, and the member invoker in local mode.

Every input has its label above it and a one-line hint under the label. Screen readers announce the label as the
input's name and the hint as its help text.

The inspector uses a calm variant of the G4 Gateway theme, chosen to limit eye fatigue in long sessions: near-black
surfaces, soft white text instead of pure white, few borders, 14px IBM Plex type, and the electric blue accent only
for the main action, focus, selection, and the active tab. The fonts are embedded under the SIL Open Font License,
which ships as `Fonts\OFL.txt` beside the executable. Icons come from the Windows icon font. On Windows 11 the title
bar takes the same colors. The selection frame drawn over SAP GUI stays Windows blue, and locator matches stay
orange, so they read clearly on SAP's light screens. The screen canvas of driver sessions uses electric blue for the
selection and yellow for matches.

## Sources

The source selector switches the whole window between inspection sources. Switching clears the tree, the selection,
and every highlight.

| Feature | Local (COM) | Driver session |
| --- | --- | --- |
| Tree | Live scripting tree | The driver's page source |
| Hover on the SAP GUI desktop | Yes | Only for a driver on this machine (`localhost`) |
| Screen canvas | No | Yes |
| Properties | Read over COM | Page source plus the driver's attribute and rect endpoints |
| Locators | Verified with the driver's locator code | Verified by the driver |
| Actions | Press, Select, Toggle, Set focus, Set text, Clear, Select key | The same, except Set focus |
| Virtual keys | SAP GUI's own key descriptions | The standard key list (Enter, F1 to Ctrl+Shift+F12, paging) |
| Follow focus | Yes | No; the driver has no focus endpoint |
| SAP frame for grid cells | Yes | No; the canvas frames the whole control |
| Invoke member | Yes | No; the driver does not run arbitrary scripting calls |

## Create session

**Create session** opens a dialog that connects the inspector to a driver server.

1. Enter the driver URL, by default `http://localhost:5556/wd/hub`.
2. Fill the `sap:options` form (session path, connection and session index, connection description, system name,
   client) or edit the capabilities JSON directly. Both edit the same data: form edits rewrite `sap:options` and keep
   every other capability, and valid JSON refills the form.
3. Optionally save the settings as a named set. Sets are stored in
   `%APPDATA%\G4\SapInspector\capability-sets.json` and hold selection criteria only, never credentials. Selecting a
   saved set loads it; Rename and Delete manage the list.
4. While local inspection is attached, **Prefill from this machine** fills the session path, system, and client from a
   local SAP GUI session.
5. Press **Create session**, or pick a session under **Attach to an open session** and press **Attach**. The open
   sessions come from the driver's `/status`.

**Disconnect** returns to local inspection. Sessions the inspector created are deleted; attached sessions stay open
on the driver. Closing the inspector also deletes the sessions it created.

## Live inspection

1. Locally, hovering resolves the control under the cursor with `GuiSession.findByPosition` on the session that owns
   the window under the cursor. In a driver session, the driver's `POST /session/{s}/sap/element/at` endpoint runs the
   same lookup. The cursor is sampled every 150 ms, and slow lookups are coalesced to the newest position.
2. On the desktop, the selection is framed by a click-through overlay with a type and name tag. Locally, when the hit
   is an inner object such as a grid cell, SAP GUI's own `Visualize` frame also marks the cell.
3. On the screen canvas, hovering resolves controls in the same way; clicking selects the control and pauses live
   inspection. The canvas captures a new screenshot after each action and on **Screenshot**.
4. **Follow focus** also selects the control that has the keyboard focus (`ActiveWindow.GuiFocus`), in local mode.
5. Hovering over a non-SAP window keeps the previous selection.

## Keyboard shortcuts

The shortcuts match Accessibility Insights and are registered system-wide, so they work while SAP GUI has focus.

| Shortcut | Action |
| --- | --- |
| Shift+F5 | Pause or resume live inspection |
| Ctrl+Shift+F5 | Select the previous sibling |
| Ctrl+Shift+F6 | Select the parent |
| Ctrl+Shift+F7 | Select the first child |
| Ctrl+Shift+F8 | Select the next sibling |
| Ctrl+Shift+F9 | Select the last child |

Navigation pauses live inspection, so hovering does not override the selection. When another application already
owns a shortcut, the window title says so and the shortcut works only while the inspector has focus.

## Details

1. **Properties** shows the basic set by default; **Show all** adds field, window, shell, and accessibility
   properties. The filter matches names and values, and Ctrl+C copies the selected rows. Password text is never read
   and shows as `(hidden)`. Local session rows include `Info.*` system data but never the user name.
2. **Locators** lists the relative SAP ID and XPath candidates by name, text, tooltip, and absolute ID. Each one is
   evaluated against the live session, and marked as unique or with its match count. Locators are generated when live
   inspection is paused or the selection comes from the tree, navigation, focus, or a canvas click.
3. **Actions** offers the driver's interactions for the control family.
4. **Window** sends a virtual key, maximizes the window, or reads the status bar.
5. **Invoke member** calls any scripting method, or reads or writes any property, by name. Arguments are
   comma-separated: quoted text is a string, `true` and `false` are Booleans, and numbers are integers or decimals.

Actions run only when you press their button, and each one changes the live SAP session.

## Limitations

1. Shell internals, such as grid cells and tree nodes, are reported as the shell plus an inner-object description;
   locally, use **Invoke member** for shell-specific methods such as `pressToolbarButton`.
2. Only the SAP Logon instance registered as `SAPGUI` in the running object table is visible.
3. The desktop overlay assumes SAP GUI reports screen coordinates in physical pixels. The overlay and the canvas line
   up with the [SAP GUI simulator](simulator.md) at 100% display scaling; other scaling setups and real SAP GUI have
   not been verified.
4. The direct running object table attach, including a 64-bit inspector with a 32-bit process, has been verified
   against the 32-bit SAP GUI simulator; confirm it once with SAP Logon.
