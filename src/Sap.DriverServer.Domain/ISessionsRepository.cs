using System.Collections.Generic;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Manages WebDriver sessions attached to SAP GUI sessions, and their window-level operations.
    /// </summary>
    /// <remarks>
    /// Methods return an HTTP status code with their result, following the UIA driver server repositories.
    /// SAP prerequisite and COM failures are thrown as <see cref="SapDriverException"/> and rendered by the error middleware.
    /// </remarks>
    public interface ISessionsRepository
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the active WebDriver sessions keyed by session identifier.
        /// </summary>
        IDictionary<string, SapSessionModel> Sessions { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Detaches a WebDriver session; the SAP GUI session and its windows stay open.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <returns>200 when detached; 404 when the session does not exist.</returns>
        int DeleteSession(string id);

        /// <summary>
        /// Discovers the scriptable SAP GUI connections and sessions.
        /// </summary>
        /// <returns>The status code and the connections in SAP order.</returns>
        (int StatusCode, IEnumerable<SapConnectionInfoModel> Connections) GetConnections();

        /// <summary>
        /// Gets the handle and title of the session's current window as a JSON string, matching the UIA contract.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <returns>The status code and the JSON string <c>{"handle":"…","name":"…"}</c>.</returns>
        (int StatusCode, string Handle) GetHandle(string id);

        /// <summary>
        /// Gets the hexadecimal handles of every open window of the attached SAP GUI session.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <returns>The status code and the handles in window order (<c>wnd[0]</c> first).</returns>
        (int StatusCode, IEnumerable<string> Handles) GetHandles(string id);

        /// <summary>
        /// Reports whether SAP GUI Scripting is reachable, without throwing.
        /// </summary>
        /// <returns>The reachability report.</returns>
        SapScriptingStatusModel GetScriptingStatus();

        /// <summary>
        /// Gets a session by identifier.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <returns>200 with the session, or 404 with null.</returns>
        (int StatusCode, SapSessionModel Session) GetSession(string id);

        /// <summary>
        /// Gets every active session.
        /// </summary>
        /// <returns>The status code and the sessions.</returns>
        (int StatusCode, IEnumerable<SapSessionModel> Sessions) GetSessions();

        /// <summary>
        /// Reads the status bar message of the session's main window.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <returns>The status code and the status bar message.</returns>
        (int StatusCode, SapStatusBarModel StatusBar) GetStatusBar(string id);

        /// <summary>
        /// Captures the session's current window through <c>HardCopy</c>.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <returns>The status code and the PNG image as a base64 string.</returns>
        (int StatusCode, string Entity) NewScreenshot(string id);

        /// <summary>
        /// Attaches a new WebDriver session to the SAP GUI session selected by the <c>sap:options</c> capability.
        /// </summary>
        /// <param name="newSessionModel">The W3C new session request.</param>
        /// <returns>The status code and the W3C new session response envelope.</returns>
        /// <exception cref="SapDriverException">The options are invalid, or zero or several sessions match.</exception>
        (int StatusCode, object Entity) NewSession(NewSessionModel newSessionModel);

        /// <summary>
        /// Sends a SAP virtual key to a window of the attached session.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <param name="virtualKey">The virtual key and optional target window.</param>
        /// <returns>The status code.</returns>
        int SendVirtualKey(string id, SapVirtualKeyModel virtualKey);

        /// <summary>
        /// Maximizes the session's current window.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <returns>The status code and the window rectangle after maximizing.</returns>
        (int StatusCode, RectangleModel Entity) SetWindowMaximized(string id);

        /// <summary>
        /// Selects and focuses a window of the attached session by handle, SAP window name, or title.
        /// </summary>
        /// <param name="id">The session identifier.</param>
        /// <param name="windowHandleOrName">The hexadecimal handle, the window name such as <c>wnd[1]</c>, or the exact title.</param>
        /// <returns>200 when switched; 400 when the value is empty; 404 when no window matches.</returns>
        int SwitchWindow(string id, string windowHandleOrName);
        #endregion
    }
}
