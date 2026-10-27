using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Default <see cref="ISessionsRepository"/> backed by the shared session dictionary and the scripting host.
    /// </summary>
    /// <param name="sessions">The shared dictionary of active sessions, registered as a singleton.</param>
    /// <param name="host">The scripting host that runs every SAP call on the STA thread.</param>
    /// <param name="settings">The driver settings that provide the busy wait.</param>
    /// <param name="logger">The logger; it records identifiers and kinds only, never SAP content.</param>
    public class SessionsRepository(
        IDictionary<string, SapSessionModel> sessions,
        ISapScriptingHost host,
        SapDriverSettings settings,
        ILogger<SessionsRepository> logger) : ISessionsRepository
    {
        #region *** Constants    ***
        // Deserializes sap:options leniently: any casing, and numbers sent as strings.
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Identifies the PNG format in GuiFrameWindow.HardCopy (0 BMP, 1 JPG, 2 PNG, 3 GIF).
        private const int PngImageType = 2;

        // Holds the eight-byte PNG file signature used to verify HardCopy output.
        private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        #endregion

        #region *** Fields       ***
        // Bounds how long each operation waits for a busy SAP session.
        private readonly int _busyWaitMilliseconds = settings.BusyWaitMilliseconds;

        // Runs every SAP call on the STA dispatcher thread.
        private readonly ISapScriptingHost _host = host;

        // Records lifecycle events with identifiers only.
        private readonly ILogger<SessionsRepository> _logger = logger;
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public IDictionary<string, SapSessionModel> Sessions { get; } = sessions;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public int DeleteSession(string id)
        {
            // Report unknown sessions without touching SAP GUI.
            if (!Sessions.TryGetValue(id, out var session))
            {
                _logger.LogError("Session with ID {SessionId} not found.", id);
                return StatusCodes.Status404NotFound;
            }

            // Detach only: forget the element references and the session, leaving every SAP window open.
            session.Elements.Clear();
            Sessions.Remove(id);
            _logger.LogInformation("Session {SessionId} detached from SAP session {SapSessionPath}.", id, session.SapSessionPath);

            return StatusCodes.Status200OK;
        }

        /// <inheritdoc />
        public (int StatusCode, IEnumerable<SapConnectionInfoModel> Connections) GetConnections()
        {
            // Discover on the STA thread; the result is COM-free and safe to return.
            var connections = _host.Invoke(SapGuiNavigator.GetConnections);

            return (StatusCodes.Status200OK, connections);
        }

        /// <inheritdoc />
        public (int StatusCode, string Handle) GetHandle(string id)
        {
            // Read the current window's handle and title.
            var (statusCode, handle) = InvokeOnSession(id, (sessionModel, session) =>
            {
                var window = SapGuiNavigator.GetWindow(session, sessionModel.WindowName);
                return new
                {
                    Handle = SapGuiNavigator.GetHandle(window),
                    Name = SapGuiNavigator.GetString(window, propertyName: "Text")
                };
            });

            // Serialize to the JSON string shape the UIA server returns.
            return statusCode == StatusCodes.Status200OK
                ? (statusCode, JsonSerializer.Serialize(handle, JsonOptions))
                : (statusCode, default);
        }

        /// <inheritdoc />
        public (int StatusCode, IEnumerable<string> Handles) GetHandles(string id)
        {
            // List the session's windows (wnd[0], wnd[1], …) by handle, skipping windows without one.
            return InvokeOnSession(id, (_, session) => session
                .GetChildren()
                .Select(SapGuiNavigator.GetHandle)
                .Where(i => i.Length > 0)
                .ToList()
                .AsEnumerable());
        }

        /// <inheritdoc />
        public SapScriptingStatusModel GetScriptingStatus()
        {
            try
            {
                // Count connections as a cheap reachability probe.
                var connections = _host.Invoke(application => application.GetChildren().Count);

                return new SapScriptingStatusModel
                {
                    Available = true,
                    Connections = connections
                };
            }
            catch (SapDriverException exception)
            {
                // Report the classified reason instead of failing the status endpoint.
                return new SapScriptingStatusModel
                {
                    Available = false,
                    ErrorKind = exception.ErrorKind,
                    Message = exception.Message
                };
            }
        }

        /// <inheritdoc />
        public (int StatusCode, SapSessionModel Session) GetSession(string id)
        {
            // Look up the session without touching SAP GUI.
            if (id is not null && Sessions.TryGetValue(id, out var session))
            {
                return (StatusCodes.Status200OK, session);
            }

            _logger.LogError("Session with ID {SessionId} not found.", id);
            return (StatusCodes.Status404NotFound, default);
        }

        /// <inheritdoc />
        public (int StatusCode, IEnumerable<SapSessionModel> Sessions) GetSessions()
        {
            // Return a snapshot so callers can enumerate while sessions are added or removed.
            return (StatusCodes.Status200OK, Sessions.Values.ToList());
        }

        /// <inheritdoc />
        public (int StatusCode, SapStatusBarModel StatusBar) GetStatusBar(string id)
        {
            // Read the status bar of the main window; it is the only window SAP guarantees to have one.
            return InvokeOnSession(id, (_, session) =>
            {
                var statusBar = session.FindById("wnd[0]/sbar");
                if (statusBar is null)
                {
                    return new SapStatusBarModel();
                }

                return new SapStatusBarModel
                {
                    MessageAsPopup = SapGuiNavigator.GetBoolean(statusBar, propertyName: "MessageAsPopup"),
                    MessageId = SapGuiNavigator.GetString(statusBar, propertyName: "MessageId"),
                    MessageNumber = SapGuiNavigator.GetString(statusBar, propertyName: "MessageNumber"),
                    MessageType = SapGuiNavigator.GetString(statusBar, propertyName: "MessageType"),
                    Text = SapGuiNavigator.GetString(statusBar, propertyName: "Text")
                };
            });
        }

        /// <inheritdoc />
        public (int StatusCode, string Entity) NewScreenshot(string id)
        {
            // Let SAP GUI write the image to a private temporary file; SAP Logon runs as the same user.
            var path = Path.Combine(Path.GetTempPath(), $"sap-driver-{Guid.NewGuid():N}.png");
            var writtenPath = string.Empty;

            try
            {
                // Capture the current window through HardCopy, which returns the path it actually wrote.
                var (statusCode, result) = InvokeOnSession(id, (sessionModel, session) =>
                {
                    var window = SapGuiNavigator.GetWindow(session, sessionModel.WindowName);
                    return Convert.ToString(window.InvokeMethod("HardCopy", path, PngImageType), CultureInfo.InvariantCulture);
                });

                if (statusCode != StatusCodes.Status200OK)
                {
                    return (statusCode, default);
                }

                // Read the produced file and verify it is a PNG before returning it.
                writtenPath = string.IsNullOrEmpty(result) || !File.Exists(result) ? path : result;
                var bytes = File.ReadAllBytes(writtenPath);
                AssertPng(bytes);

                return (StatusCodes.Status200OK, Convert.ToBase64String(bytes));
            }
            finally
            {
                // Remove the temporary image in every outcome, because it can show business data.
                RemoveFile(path);
                RemoveFile(writtenPath);
            }

            // Rejects HardCopy output that is not a PNG instead of returning an image of another format.
            static void AssertPng(byte[] bytes)
            {
                var isPng = bytes.Length >= PngSignature.Length && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);
                if (!isPng)
                {
                    throw new SapDriverException(
                        WebDriverErrorCodes.UnknownError,
                        SapErrorKinds.ComFailure,
                        StatusCodes.Status500InternalServerError,
                        message: "SAP GUI HardCopy did not produce a PNG image.");
                }
            }
        }

        /// <inheritdoc />
        public (int StatusCode, object Entity) NewSession(NewSessionModel newSessionModel)
        {
            // Merge capabilities the same way as the UIA server: alwaysMatch, else the first firstMatch entry.
            var capabilities = GetMatchCapabilities(newSessionModel);
            var options = ConvertToOptions(capabilities);
            SessionSelector.AssertOptions(options);

            // Discover the open sessions and select exactly one; ambiguity and absence are thrown as classified errors.
            var connections = _host.Invoke(SapGuiNavigator.GetConnections);
            var selected = SessionSelector.SelectSession(connections, options);

            // Record the attachment; the session holds identifiers only, never COM references.
            var sessionCapabilities = new Dictionary<string, object>(capabilities, StringComparer.OrdinalIgnoreCase)
            {
                [SapCapabilities.Session] = new
                {
                    selected.Id,
                    selected.SystemName,
                    selected.Client,
                    selected.ConnectionDescription
                }
            };
            var session = new SapSessionModel
            {
                Capabilities = sessionCapabilities,
                SapSessionPath = selected.Id,
                SessionId = $"{Guid.NewGuid()}"
            };
            Sessions[session.SessionId] = session;

            _logger.LogInformation(
                "Session {SessionId} attached to SAP session {SapSessionPath} (system {SystemName}, client {Client}).",
                session.SessionId,
                selected.Id,
                selected.SystemName,
                selected.Client);

            // Return the W3C new session envelope.
            var response = new WebDriverResponseModel
            {
                Value = new SessionResponseModel
                {
                    Capabilities = sessionCapabilities,
                    SessionId = session.SessionId
                }
            };

            return (StatusCodes.Status200OK, response);
        }

        /// <inheritdoc />
        public int SendVirtualKey(string id, SapVirtualKeyModel virtualKey)
        {
            // Require the payload; model validation normally guarantees it for HTTP callers.
            ArgumentNullException.ThrowIfNull(argument: virtualKey);

            // Send the key to the requested window, or to the session's current window.
            var (statusCode, _) = InvokeOnSession(id, (sessionModel, session) =>
            {
                var windowName = string.IsNullOrWhiteSpace(virtualKey.Window) ? sessionModel.WindowName : virtualKey.Window.Trim();
                var window = SapGuiNavigator.GetWindow(session, windowName);
                window.InvokeMethod("sendVKey", virtualKey.VirtualKey);

                return true;
            });

            return statusCode;
        }

        /// <inheritdoc />
        public (int StatusCode, RectangleModel Entity) SetWindowMaximized(string id)
        {
            // Maximize the current window and report its new bounds.
            return InvokeOnSession(id, (sessionModel, session) =>
            {
                var window = SapGuiNavigator.GetWindow(session, sessionModel.WindowName);
                window.InvokeMethod("maximize");

                return SapGuiNavigator.GetRectangle(window);
            });
        }

        /// <inheritdoc />
        public int SwitchWindow(string id, string windowHandleOrName)
        {
            // Reject an empty target, matching the UIA server's invalid-argument status.
            if (string.IsNullOrWhiteSpace(windowHandleOrName))
            {
                return StatusCodes.Status400BadRequest;
            }

            // Find the window by handle, name, or title and focus it.
            var target = windowHandleOrName.Trim();
            var (statusCode, windowName) = InvokeOnSession(id, (sessionModel, session) =>
            {
                foreach (var window in session.GetChildren())
                {
                    var name = window.Id[(window.Id.LastIndexOf('/') + 1)..];
                    if (TestWindow(window, name, target))
                    {
                        // Focus the window and remember it so window-scoped operations target it.
                        window.InvokeMethod("setFocus");
                        sessionModel.WindowName = name;

                        return name;
                    }
                }

                return null;
            });

            if (statusCode != StatusCodes.Status200OK)
            {
                return statusCode;
            }

            // Report a target that matched no open window.
            return windowName is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK;
        }

        // Converts the sap:options capability to the options model; malformed JSON is an invalid argument.
        private static SapOptionsModel ConvertToOptions(Dictionary<string, object> capabilities)
        {
            // Default to "no criteria" when the capability is absent.
            if (!capabilities.TryGetValue(SapCapabilities.Options, out var value) || value is null)
            {
                return new SapOptionsModel();
            }

            try
            {
                // Round-trip through JSON so JsonElement and dictionary inputs bind the same way.
                return JsonSerializer.Deserialize<SapOptionsModel>(JsonSerializer.Serialize(value), JsonOptions) ?? new SapOptionsModel();
            }
            catch (JsonException exception)
            {
                const string message = "The sap:options capability is not a valid options object.";
                throw new SapDriverException(
                    WebDriverErrorCodes.InvalidArgument,
                    errorKind: null,
                    StatusCodes.Status400BadRequest,
                    message,
                    data: null,
                    innerException: exception);
            }
        }

        // Selects the capability set used for session creation, following the UIA server's merge rule.
        private static Dictionary<string, object> GetMatchCapabilities(NewSessionModel newSessionModel)
        {
            // Prefer alwaysMatch; fall back to the first firstMatch entry; otherwise use no capabilities.
            var alwaysMatch = newSessionModel?.Capabilities?.AlwaysMatch;
            var firstMatch = newSessionModel?.Capabilities?.FirstMatch;

            if (alwaysMatch is { Count: > 0 })
            {
                return alwaysMatch;
            }

            return firstMatch is { Length: > 0 } && firstMatch[0] is not null
                ? firstMatch[0]
                : new Dictionary<string, object>();
        }

        // Removes a temporary file, ignoring absent paths and files still locked by SAP GUI.
        private static void RemoveFile(string path)
        {
            // Skip empty paths and files that were never written.
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // The file is still locked; the temporary folder cleanup removes it later.
            }
        }

        // Tests whether a window matches a switch target by handle (with or without 0x), window name, or exact title.
        private static bool TestWindow(ISapGuiNode window, string name, string target)
        {
            // Compare handles in hexadecimal, ignoring an optional 0x prefix and casing.
            var handle = SapGuiNavigator.GetHandle(window);
            var normalizedTarget = target.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? target[2..] : target;
            var isHandleMatch = handle.Length > 0 && string.Equals(handle, normalizedTarget.TrimStart('0'), StringComparison.OrdinalIgnoreCase);
            var isNameMatch = string.Equals(name, target, StringComparison.OrdinalIgnoreCase);

            return isHandleMatch || isNameMatch || string.Equals(SapGuiNavigator.GetString(window, propertyName: "Text"), target, StringComparison.Ordinal);
        }

        // Runs an operation against the idle SAP session attached to a WebDriver session.
        // The operation receives the session state and the resolved GuiSession node; it runs on the STA thread.
        // Returns 404 without touching SAP GUI when the WebDriver session does not exist.
        private (int StatusCode, T Result) InvokeOnSession<T>(string id, Func<SapSessionModel, ISapGuiNode, T> operation)
        {
            // Validate the WebDriver session before queueing STA work.
            var (statusCode, sessionModel) = GetSession(id);
            if (statusCode != StatusCodes.Status200OK)
            {
                return (statusCode, default);
            }

            // Resolve the SAP session, wait while it is busy, and run the operation.
            var busyWait = _busyWaitMilliseconds;
            var result = _host.Invoke(application =>
            {
                var session = SapGuiNavigator.GetSession(application, sessionModel, busyWait);
                return operation(sessionModel, session);
            });

            return (StatusCodes.Status200OK, result);
        }
        #endregion
    }
}
