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
