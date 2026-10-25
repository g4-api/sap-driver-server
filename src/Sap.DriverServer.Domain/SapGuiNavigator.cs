using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Provides typed property access and session, window, and connection navigation over <see cref="ISapGuiNode"/>.
    /// </summary>
    /// <remarks>
    /// Every method runs inside a <see cref="ISapScriptingHost"/> operation, on the dispatcher thread, and returns
    /// COM-free values. Property readers treat members the object does not expose as absent instead of failing,
    /// because SAP control families expose different subsets of the common properties.
    /// </remarks>
    public static class SapGuiNavigator
    {
        #region *** Constants    ***
        // Spaces busy-state polls so waiting does not saturate the SAP GUI process with cross-process calls.
        private static readonly TimeSpan BusyPollInterval = TimeSpan.FromMilliseconds(100);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Finds the control under a screen point in a session through <c>findByPosition</c>.
        /// </summary>
        /// <param name="application">The <c>GuiApplication</c> node, used to resolve absolute identifiers.</param>
        /// <param name="session">The session whose windows are hit-tested.</param>
        /// <param name="x">The horizontal screen coordinate in pixels.</param>
        /// <param name="y">The vertical screen coordinate in pixels.</param>
        /// <returns>
        /// The control and the inner-object description (such as a grid cell, or empty), or a null node when the point
        /// is outside the session's windows or on window chrome.
        /// </returns>
        /// <remarks>
        /// SAP GUI returns a collection of the control identifier and an inner-object description. The identifier is
        /// absolute or session-relative depending on the SAP GUI release, so both forms are resolved.
        /// </remarks>
        public static (ISapGuiNode Node, string InnerObject) FindByPosition(ISapGuiNode application, ISapGuiNode session, int x, int y)
        {
            // Hit-test with raise=false: an empty result means the point is on window chrome or outside SAP GUI.
            var items = session.InvokeCollectionMethod("findByPosition", x, y, false);
            var hitId = items.Count > 0 ? Convert.ToString(items[0], CultureInfo.InvariantCulture) : string.Empty;
            if (string.IsNullOrEmpty(hitId))
            {
                return (null, string.Empty);
            }

            // Resolve the identifier in its form and keep the inner-object description for cell-level highlighting.
            var node = hitId.StartsWith("/app", StringComparison.Ordinal) ? application.FindById(hitId) : session.FindById(hitId);
            var innerObject = items.Count > 1 ? Convert.ToString(items[1], CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;

            return (node, innerObject);
        }

        /// <summary>
        /// Gets the scriptable connections and their sessions, as COM-free discovery models.
        /// </summary>
        /// <param name="application">The <c>GuiApplication</c> node.</param>
        /// <returns>The connections in SAP order.</returns>
        public static IList<SapConnectionInfoModel> GetConnections(ISapGuiNode application)
        {
            // Walk application > connections > sessions, reading identifiers and system metadata only.
            var connections = new List<SapConnectionInfoModel>();

            foreach (var connectionNode in application.GetChildren())
            {
                // Read the connection metadata; a server-disabled connection hides its sessions from scripting.
                var connection = new SapConnectionInfoModel
                {
                    Description = GetString(connectionNode, propertyName: "Description"),
                    Id = connectionNode.Id,
                    Index = GetIndex(connectionNode.Id, segment: "con"),
                    ScriptingDisabledByServer = GetBoolean(connectionNode, propertyName: "DisabledByServer")
                };
                connections.Add(connection);

                if (connection.ScriptingDisabledByServer)
                {
                    continue;
                }

                // Map each session; GuiSessionInfo provides the system and client used for selection.
                foreach (var sessionNode in connectionNode.GetChildren())
                {
                    var info = sessionNode.GetNode("Info");
                    connection.Sessions.Add(new SapSessionInfoModel
                    {
                        Busy = GetBoolean(sessionNode, propertyName: "Busy"),
                        Client = info is null ? string.Empty : GetString(info, propertyName: "Client"),
                        ConnectionDescription = connection.Description,
                        ConnectionIndex = connection.Index,
                        Id = sessionNode.Id,
                        Index = GetIndex(sessionNode.Id, segment: "ses"),
                        SystemName = info is null ? string.Empty : GetString(info, propertyName: "SystemName")
                    });
                }
            }

            return connections;
        }

        /// <summary>
        /// Gets a Boolean property, treating absent or non-Boolean values as false.
        /// </summary>
        /// <param name="node">The node to read.</param>
        /// <param name="propertyName">The SAP GUI Scripting property name.</param>
        /// <returns>The property value, or false when the property is absent.</returns>
        public static bool GetBoolean(ISapGuiNode node, string propertyName)
        {
            // Convert only real Boolean values; strings such as "X" are not interpreted.
            return TestProperty(node, propertyName, out var value) && value is bool isSet && isSet;
        }

        /// <summary>
        /// Gets the hexadecimal window handle of a frame window, in the format used by the UIA driver server.
        /// </summary>
        /// <param name="window">The <c>GuiFrameWindow</c> node.</param>
        /// <returns>The handle in uppercase hexadecimal, or an empty string when the window exposes no handle.</returns>
        public static string GetHandle(ISapGuiNode window)
        {
            // Format the numeric handle as uppercase hexadecimal, matching IntPtr.ToString("X") in the UIA server.
            var handle = GetInt64(window, propertyName: "Handle");

            return handle == 0 ? string.Empty : handle.ToString("X", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses the zero-based index of a SAP identifier segment, such as <c>con[2]</c> or <c>ses[0]</c>.
        /// </summary>
        /// <param name="id">The SAP GUI Scripting identifier.</param>
        /// <param name="segment">The segment prefix, such as <c>con</c> or <c>ses</c>.</param>
        /// <returns>The index, or <see cref="SapOptionsModel.NoIndex"/> when the segment is absent.</returns>
        public static int GetIndex(string id, string segment)
        {
            // Read the last occurrence so nested identifiers resolve to their own segment.
            var matches = Regex.Matches(id ?? string.Empty, $@"(?:^|/){Regex.Escape(segment)}\[(\d+)\]");
            if (matches.Count == 0)
            {
                return SapOptionsModel.NoIndex;
            }

            return int.Parse(matches[^1].Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets an integer property, treating absent or non-numeric values as zero.
        /// </summary>
        /// <param name="node">The node to read.</param>
        /// <param name="propertyName">The SAP GUI Scripting property name.</param>
        /// <returns>The property value, or zero when the property is absent.</returns>
        public static int GetInt32(ISapGuiNode node, string propertyName)
        {
            // Narrow the 64-bit reader; screen coordinates and sizes fit in 32 bits.
            return (int)GetInt64(node, propertyName);
        }

        /// <summary>
        /// Gets the screen rectangle of a visual node from <c>ScreenLeft</c>, <c>ScreenTop</c>, <c>Width</c>, and <c>Height</c>.
        /// </summary>
        /// <param name="node">The visual node.</param>
        /// <returns>The edge-based rectangle.</returns>
        public static RectangleModel GetRectangle(ISapGuiNode node)
        {
            // Convert SAP's origin-and-size geometry to the edge-based rectangle shared with the UIA server.
            var left = GetInt32(node, propertyName: "ScreenLeft");
            var top = GetInt32(node, propertyName: "ScreenTop");

            return new RectangleModel
            {
                Bottom = top + GetInt32(node, propertyName: "Height"),
                Left = left,
                Right = left + GetInt32(node, propertyName: "Width"),
                Top = top
            };
        }

        /// <summary>
        /// Gets a property as a string, treating absent values as empty.
        /// </summary>
        /// <param name="node">The node to read.</param>
        /// <param name="propertyName">The SAP GUI Scripting property name.</param>
        /// <returns>The invariant string form of the value, or an empty string when the property is absent.</returns>
        public static string GetString(ISapGuiNode node, string propertyName)
        {
            // Format Booleans in lowercase so attribute values read the same as in JSON.
            if (!TestProperty(node, propertyName, out var value) || value is null)
            {
                return string.Empty;
            }

            return value is bool isSet
                ? (isSet ? "true" : "false")
                : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Resolves the attached SAP GUI session node and waits until it is idle.
        /// </summary>
        /// <param name="application">The <c>GuiApplication</c> node.</param>
        /// <param name="sessionModel">The WebDriver session state that names the SAP session.</param>
        /// <param name="busyWaitMilliseconds">The maximum time to wait while the session is busy.</param>
        /// <returns>The idle <c>GuiSession</c> node.</returns>
        /// <exception cref="SapDriverException">The SAP session was closed, or stayed busy beyond the wait.</exception>
        public static ISapGuiNode GetSession(ISapGuiNode application, SapSessionModel sessionModel, int busyWaitMilliseconds)
        {
            // Resolve the session by its scripting identifier; a closed SAP session no longer resolves.
            var session = application.FindById(sessionModel.SapSessionPath);
            if (session is null)
            {
                var message = $"The attached SAP GUI session '{sessionModel.SapSessionPath}' is no longer open.";
                throw new SapDriverException(
                    WebDriverErrorCodes.NoSuchWindow,
                    SapErrorKinds.NoMatchingSession,
                    StatusCodes.Status404NotFound,
                    message);
            }

            // Wait for any running server round trip to finish before issuing further calls.
            WaitWhileBusy(session, busyWaitMilliseconds);

            return session;
        }

        /// <summary>
        /// Resolves the window used by window-scoped operations.
        /// </summary>
        /// <param name="session">The <c>GuiSession</c> node.</param>
        /// <param name="windowName">The window name selected through "Switch To Window", or null or empty for the active window.</param>
        /// <returns>The <c>GuiFrameWindow</c> node.</returns>
        /// <exception cref="SapDriverException">The named window is not open.</exception>
        public static ISapGuiNode GetWindow(ISapGuiNode session, string windowName)
        {
            // Use the active window unless the client switched to a specific one.
            var window = string.IsNullOrEmpty(windowName)
                ? session.GetNode("ActiveWindow")
                : session.FindById(windowName);

            if (window is null)
            {
                var message = $"The SAP GUI window '{windowName}' is not open in the attached session.";
                throw new SapDriverException(
                    WebDriverErrorCodes.NoSuchWindow,
                    errorKind: null,
                    StatusCodes.Status404NotFound,
                    message);
            }

            return window;
        }

        /// <summary>
        /// Tests whether a node exposes a property and reads its value.
        /// </summary>
        /// <param name="node">The node to read.</param>
        /// <param name="propertyName">The SAP GUI Scripting property name.</param>
        /// <param name="value">The property value when exposed; otherwise null.</param>
        /// <returns>True when the property is exposed; otherwise false.</returns>
        public static bool TestProperty(ISapGuiNode node, string propertyName, out object value)
        {
            try
            {
                // Read the property; only "not exposed" failures are treated as absence.
                value = node.GetProperty(propertyName);
                return true;
            }
            catch (SapDriverException exception) when (exception.ErrorKind == SapErrorKinds.NotExposedByScripting)
            {
                value = null;
                return false;
            }
        }

        /// <summary>
        /// Waits until a session reports <c>Busy = false</c>.
        /// </summary>
        /// <param name="session">The <c>GuiSession</c> node.</param>
        /// <param name="busyWaitMilliseconds">The maximum time to wait.</param>
        /// <exception cref="SapDriverException">The session stayed busy beyond the wait.</exception>
        public static void WaitWhileBusy(ISapGuiNode session, int busyWaitMilliseconds)
        {
            // Poll the busy flag until it clears or the budget is spent; the STA thread is blocked meanwhile by design.
            var stopwatch = Stopwatch.StartNew();

            while (GetBoolean(session, propertyName: "Busy"))
            {
                if (stopwatch.ElapsedMilliseconds >= busyWaitMilliseconds)
                {
                    var message = $"The SAP GUI session stayed busy for more than {busyWaitMilliseconds} ms.";
                    throw new SapDriverException(
                        WebDriverErrorCodes.UnknownError,
                        SapErrorKinds.SessionBusy,
                        StatusCodes.Status500InternalServerError,
                        message);
                }

                Thread.Sleep(BusyPollInterval);
            }
        }

        // Reads an integral property as a 64-bit value; absent and non-numeric values yield zero.
        // Window handles are reported by SAP as 32-bit integers but are widened to avoid sign surprises.
        private static long GetInt64(ISapGuiNode node, string propertyName)
        {
            // Accept every numeric COM variant type; ignore strings and empty values.
            if (!TestProperty(node, propertyName, out var value) || value is null or string or bool)
            {
                return 0;
            }

            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
