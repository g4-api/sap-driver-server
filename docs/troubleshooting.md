# Troubleshooting

SAP and COM failures are returned as W3C errors with a stable `data.sapErrorKind` value. The message explains the
cause without quoting SAP screen contents; the server log records the kind and the COM HRESULT.

## Error kinds

| `sapErrorKind` | W3C error | Cause | Fix |
| --- | --- | --- | --- |
| `sapGuiNotRunning` | `session not created` or `unknown error` | No SAP Logon of this Windows user and integrity level is running, or it exited during the session | Start SAP Logon as the same user, elevated or not like the server, then create a new session |
| `scriptingDisabled` | `session not created` | Scripting is disabled in the SAP GUI options | Enable it in SAP GUI Options > Accessibility & Scripting |
| `scriptingDisabledByServer` | `session not created` | The application server disallows scripting | Ask the SAP administrator to set `sapgui/user_scripting` |
| `noMatchingSession` | `session not created` or `no such window` | No open session matches `sap:options`, or the attached session closed | Check `GET /sap/sessions` and adjust `sap:options` |
| `ambiguousSession` | `session not created` | Several sessions match `sap:options` | Add `sessionPath`, or `connectionIndex` and `sessionIndex` |
| `accessDenied` | `session not created` or `unknown error` | The server runs as another user or integrity level than SAP Logon | Run both as the same user in the same desktop session |
| `sessionBusy` | `unknown error` | The session stayed busy longer than `BusyWaitMilliseconds` | Wait for the server round trip, or raise the setting |
| `timeout` | `timeout` | A COM call took longer than `ComCallTimeoutMilliseconds` | Check for modal SAP dialogs, or raise the setting |
| `notExposedByScripting` | `unsupported operation` or `element not interactable` | The control or interaction has no scripting member | Use another control, an SAP-only route, or a different interaction |
| `comFailure` | `unknown error` or `invalid argument` | Any other COM error; the HRESULT is in the message | Read the server log for the HRESULT and SAP GUI state |

Errors without SAP involvement keep the UIA shape: `invalid session id`, `no such element`,
`stale element reference`, `invalid selector`, and `Bad Request` for model validation failures.

## Common situations

1. **`/status` shows `available: false`.** Read `sap.errorKind` in the same response; it uses the kinds above.
2. **A confirmation dialog appears for every request.** Clear the "Notify when a script attaches to SAP GUI" and
   "Notify when a script opens a connection" options in the SAP GUI scripting settings.
3. **A click returns `unsupported operation` on a grid or tree.** These are `GuiShell` controls whose rows and nodes
   are not scripting objects. The driver does not emulate clicks on them.
4. **A reference turns stale after navigation.** SAP rebuilds the screen on every round trip; locate the control again.
5. **A request times out while a popup is open.** A modal window blocks the main window. Switch to `wnd[1]` with
   `POST /session/{s}/window`, handle the popup, then switch back to `wnd[0]`.
6. **The first SAP Logon instance is used although several are open.** Only the instance registered as `SAPGUI` in the
   running object table is reachable through scripting; close the extra SAP Logon processes.
7. **`sapGuiNotRunning` although SAP Logon is open.** The server and SAP Logon run at different integrity levels, for
   example the server in an elevated terminal. Run both elevated or both not elevated, as the same Windows user.
8. **`scriptingDisabled` although scripting is enabled in the SAP GUI options.** The SAP GUI installation may lack the
   SAP GUI Scripting component; check the component selection in the SAP GUI installer.

## Where each error kind comes from

The table lists the exact message text, the HTTP status and the place in the code. Paths are relative to the `src` folder. Messages that include a value show it as `<value>`.

| `sapErrorKind` | Message text | HTTP | Source |
| --- | --- | --- | --- |
| `sapGuiNotRunning` | SAP GUI is not running for this Windows user. Start SAP Logon, as the same user and integrity level (elevated or not) as this process, and open a connection. | 500 | `Sap.DriverServer.Scripting/ComSapGuiEngine.cs:225-239` |
| `accessDenied` | Access to SAP GUI Scripting was denied. Run this process as the same Windows user and integrity level (elevated or not) as SAP Logon. | 500 | `Sap.DriverServer.Scripting/ComSapGuiEngine.cs:176-183` |
| `scriptingDisabled` | SAP GUI Scripting is disabled. Enable it in SAP GUI Options > Accessibility & Scripting > Scripting, or ask your SAP administrator. | 500 | `Sap.DriverServer.Scripting/ComSapGuiEngine.cs:186-193` |
| `scriptingDisabledByServer` | SAP GUI Scripting is disabled by the SAP application server for the selected connection. Ask your SAP administrator to set the profile parameter sapgui/user_scripting. | 500 | `Sap.DriverServer.Domain/SessionSelector.cs:128-133` |
| `noMatchingSession` | No SAP GUI connection is open. Open a connection in SAP Logon and log on before creating a session. | 500 | `Sap.DriverServer.Domain/SessionSelector.cs:95-99` |
| `noMatchingSession` | No open SAP GUI session matches the requested selection. | 500 | `Sap.DriverServer.Domain/SessionSelector.cs:137-139` |
| `noMatchingSession` | The attached SAP GUI session '<path>' is no longer open. | 404 (`no such window`) | `Sap.DriverServer.Domain/SapGuiNavigator.cs:215-222` |
| `ambiguousSession` | <n> SAP GUI sessions match the requested selection. Set sap:options.sessionPath, or connectionIndex and sessionIndex, to choose one. | 500 | `Sap.DriverServer.Domain/SessionSelector.cs:117-124` |
| `sessionBusy` | The SAP GUI session stayed busy for more than <n> ms. | 500 | `Sap.DriverServer.Domain/SapGuiNavigator.cs:293-300` |
| `timeout` | The message of the dispatcher timeout | 500 | `Sap.DriverServer.Domain/SapErrorMapper.cs:104-112` |
| `notExposedByScripting` | The '<interaction>' interaction is not available for SAP control type '<type>'. | 400 (`element not interactable`) | `Sap.DriverServer.Domain/SapControlInteractions.cs:266-276` |
| `comFailure` | SAP GUI Scripting call failed (comFailure, HRESULT 0x<code>). | 500, or 400 for `invalid argument` | `Sap.DriverServer.Domain/SapErrorMapper.cs:127-145` |
| `comFailure` | SAP GUI HardCopy did not produce a PNG image. | 500 | `Sap.DriverServer.Domain/SessionsRepository.cs:228-237` |

Every SAP error also carries the kind in `value.data.sapErrorKind`, which the error middleware adds from the exception (`Sap.DriverServer/Middlewares/ErrorHandlingMiddleware.cs:125-130`).

## Scripting disabled on the client or on the server

The two kinds need different people to fix them.

### scriptingDisabled: the SAP GUI client refuses scripting

**Cause:** The server asked SAP Logon for its scripting engine and got a refusal or nothing. The server reports every COM refusal other than access denied as this kind, because that is the documented cause. It is reported when the session is created and in `GET /status`.

**Check:** Read `sap.errorKind` and `sap.message` in the `GET /status` answer. Open SAP GUI Options > Accessibility & Scripting > Scripting. If scripting is enabled there, see situation 8 above.

**Fix:** Enable scripting in the SAP GUI options of the client, or install the SAP GUI Scripting component.

**Source:** `Sap.DriverServer.Scripting/ComSapGuiEngine.cs:155-170`, `Sap.DriverServer.Domain/SessionsRepository.cs:121-145`

### scriptingDisabledByServer: the SAP application server refuses scripting

**Cause:** The server reads the `DisabledByServer` property of every connection. A connection with that flag set is skipped when sessions are listed. The error is raised only when no other session matches and a disabled connection fits the `sap:options` you sent.

**Check:** Call `GET /sap/sessions` and compare the connections. A connection that works for others while yours does not points to a server-side setting for that system. `GET /status` does not show this kind, because it counts connections only.

**Fix:** Ask the SAP administrator to set the profile parameter `sapgui/user_scripting` on the application server. Changing client options does not help.

**Source:** `Sap.DriverServer.Domain/SapGuiNavigator.cs:72-86`, `Sap.DriverServer.Domain/SessionSelector.cs:101-133`

## COM attach failures

The server attaches to SAP GUI in three steps. Each step has its own failure.

### Step 1: the SAPGUI entry is missing from the running object table

**Cause:** The server looks up the entry named `SAPGUI` directly. SAP Logon registers it while it runs, and only for the same Windows user and integrity level. A missing entry, or a COM failure while reading the table, gives `sapGuiNotRunning`.

**Check:** Confirm SAP Logon is open. Compare the user and the elevation of SAP Logon and the server in Task Manager.

**Fix:** Start SAP Logon as the same user and with the same elevation as the server, then create a new session.

**Source:** `Sap.DriverServer.Scripting/ComSapGuiEngine.cs:40, 215-241`

### Step 2: Windows denies access to the COM object

**Cause:** The COM call fails with HRESULT `0x80070005`. The server maps it to `accessDenied`, in the ROT lookup and in the scripting engine request.

**Check:** Same user and integrity level check as in step 1.

**Fix:** Run the server and SAP Logon as the same user in the same desktop session.

**Source:** `Sap.DriverServer.Scripting/ComSapGuiEngine.cs:164-167, 214-235`, `Sap.DriverServer.Domain/SapErrorMapper.cs:54`

### Step 3: a later call loses its connection

**Cause:** A call on an attached session fails with one of the HRESULT values below. The first row of the table maps to `sapGuiNotRunning`, and the server then drops its cached application (`engine.Reset()`), so the next request attaches again.

**Check:** Read the HRESULT in the message "SAP GUI Scripting call failed (<kind>, HRESULT 0x<code>)".

**Fix:** See the table. After a disconnect, create a new session.

**Source:** `Sap.DriverServer.Domain/SapErrorMapper.cs:29-84, 138-145`, `Sap.DriverServer.Domain/SapScriptingHost.cs:120-130`

| HRESULT | Name in the code | Maps to | W3C error |
| --- | --- | --- | --- |
| `0x800706BE`, `0x80010108`, `0x800401FD`, `0x800706BA`, `0x800401E3` | `CallFailed`, `Disconnected`, `ObjectNotConnected`, `ServerUnavailable`, `MonikerUnavailable` | `sapGuiNotRunning` | `unknown error` |
| `0x80070005` | `AccessDenied` | `accessDenied` | `unknown error` |
| `0x80010001`, `0x8001010A` | `CallRejected`, `RetryLater` | `sessionBusy` | `unknown error` |
| `0x80020003`, `0x80020006` | `MemberNotFound`, `UnknownName` | `notExposedByScripting` | `unsupported operation` |
| `0x8002000E`, `0x80020005` | `BadParameterCount`, `TypeMismatch` | `comFailure` | `invalid argument` (HTTP 400) |
| any other | none | `comFailure` | `unknown error` |

## Timeouts and busy sessions

### timeout and sessionBusy settings

**Cause:** `BusyWaitMilliseconds` (default 10000) is how long a request waits for a busy SAP GUI session. `ComCallTimeoutMilliseconds` (default 60000) is how long a request waits for one queued COM operation, including time spent behind earlier operations, because every COM call runs on one thread. A timed-out operation keeps running on that thread and its result is dropped.

**Check:** Look for a modal SAP dialog or a long server round trip. Read the `SapDriver` section of the server `appsettings.json`.

**Fix:** Close the dialog, or raise the value in the `SapDriver` section.

**Source:** `Sap.DriverServer.Models/SapDriverSettings.cs:14-30`, `Sap.DriverServer/appsettings.json:15-18`
