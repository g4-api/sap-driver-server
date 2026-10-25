/*
 * RESOURCES
 * https://help.sap.com/docs/sap_gui_for_windows/b47d018c3b9b45e897faf66a6c0885a8/a4e022f6c155414d9c8ae8eba422cac5.html
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Provides every inspection, discovery, and ad-hoc interaction the inspector performs against SAP GUI.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each public method is one <see cref="ISapScriptingHost"/> operation, so all COM calls run on the shared STA
    /// dispatcher thread and return COM-free models. Methods block until SAP GUI answers; view models call them from
    /// background tasks so the UI thread never waits on SAP GUI.
    /// </para>
    /// <para>
    /// The hit test maps the top-level window under the cursor to its SAP session through a handle cache that is
    /// rebuilt on a miss, at most once per <see cref="WindowRefreshMilliseconds"/>, so hovering over non-SAP windows
    /// does not enumerate SAP GUI on every cursor sample. The cache and the virtual-key cache are touched only inside
    /// scripting operations, which the dispatcher serializes.
    /// </para>
    /// <para>
    /// Failures surface as <see cref="SapDriverException"/> from the host. Password text is never read.
    /// </para>
    /// </remarks>
    public sealed class InspectorService
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the minimum interval between two rebuilds of the window-to-session cache.
        /// </summary>
        public const int WindowRefreshMilliseconds = 1000;

        /// <summary>
        /// Specifies how many matches the locator tester returns, because each one becomes an overlay window.
        /// </summary>
        public const int MaximumLocatorMatches = 50;
        #endregion

        #region *** Fields       ***
        // Supplies the current time in milliseconds; injected so tests control cache expiry.
        private readonly Func<long> _clock;

        // Runs every SAP call on the STA dispatcher thread.
        private readonly ISapScriptingHost _host;

        // Caches the virtual-key descriptions per session, because reading them takes one call per key.
        private readonly Dictionary<string, IList<VirtualKeyEntry>> _virtualKeys = new(StringComparer.Ordinal);

        // Maps hexadecimal frame window handles to session identifiers for the hover hit test.
        private readonly Dictionary<string, string> _windowSessions = new(StringComparer.OrdinalIgnoreCase);

        // Records when the window cache was last rebuilt, in clock milliseconds; long.MinValue forces the first rebuild.
        private long _windowRefreshTime = long.MinValue;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="InspectorService"/> class using the system tick count as clock.
        /// </summary>
        /// <param name="host">The scripting host shared by the whole inspector.</param>
        public InspectorService(ISapScriptingHost host)
            : this(host, clock: () => Environment.TickCount64)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InspectorService"/> class with an explicit clock.
        /// </summary>
        /// <param name="host">The scripting host shared by the whole inspector.</param>
        /// <param name="clock">The clock in milliseconds used for window cache expiry.</param>
        public InspectorService(ISapScriptingHost host, Func<long> clock)
        {
            // Assert both collaborators; the service has no fallback for either.
            AssertArguments(host, clock);

            // Keep the host and the clock.
            _host = host;
            _clock = clock;

            return;

            // Requires the scripting host and the clock before the service stores them.
            // The helper reads its parameters only and reports a missing one through its argument name.
            static void AssertArguments(ISapScriptingHost host, Func<long> clock)
            {
                // Require both collaborators.
                ArgumentNullException.ThrowIfNull(argument: host);
                ArgumentNullException.ThrowIfNull(argument: clock);
            }
        }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Finds the objects a driver locator matches in a session, for the locator tester.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="locator">The locator, evaluated exactly as the driver evaluates it.</param>
        /// <returns>Up to 50 matches with their geometry, in document order.</returns>
        public IList<HitResult> FindByLocator(string sessionId, LocationStrategyModel locator)
        {
            return _host.Invoke(application =>
            {
                // Evaluate within the session, like a WebDriver find elements call.
                var session = application.FindById(sessionId);
                if (session is null)
                {
                    return new List<HitResult>();
                }

                // Resolve each match for its geometry and summary, bounded for the overlay pool.
                return ElementLocator
                    .FindElements(application, session, locator)
                    .Take(MaximumLocatorMatches)
                    .Select(i => application.FindById(i.SapId))
                    .Where(i => i is not null)
                    .Select(i => NewHitResult(i, innerObject: string.Empty))
                    .ToList();
            });
        }

        /// <summary>
        /// Generates driver locators for an object and verifies how many objects each one matches.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <returns>The candidates, most specific last; empty when the object is gone or above session level.</returns>
        public IList<LocatorCandidate> GenerateLocators(string id)
        {
            return _host.Invoke(application =>
            {
                // Locators are session-relative, so objects above session level get none.
                var sessionId = LocatorCandidates.GetSessionId(id);
                var node = application.FindById(id);
                var session = sessionId.Length == 0 ? null : application.FindById(sessionId);
                if (node is null || session is null)
                {
                    return new List<LocatorCandidate>();
                }

                // Build the shared candidates from page-source attributes, then count matches as the driver does.
                var type = node.Type;
                return LocatorCandidates
                    .NewCandidates(node.Id, type, getAttribute: i => DocumentObjectModelFactory.GetAttributeValue(node, type, i))
                    .Select(i => CountMatches(application, session, candidate: i))
                    .ToList();
            });
        }

        /// <summary>
        /// Gets the child summaries of an object for the lazily expanded tree.
        /// </summary>
        /// <param name="id">The parent identifier; <c>/app</c> or empty for the application.</param>
        /// <returns>The children in SAP order; empty when the parent is gone.</returns>
        public IList<ElementSummary> GetChildren(string id)
        {
            return _host.Invoke(application =>
            {
                // Resolve the parent; the application is the root and is not reachable through findById.
                var parent = string.IsNullOrEmpty(id) || id == application.Id ? application : application.FindById(id);

                return parent is null
                    ? new List<ElementSummary>()
                    : parent.GetChildren().Select(NewSummary).ToList();
            });
        }

        /// <summary>
        /// Gets the open connections and their sessions, for prefilling driver session options.
        /// </summary>
        /// <returns>The connections in SAP order; identifiers and system metadata only.</returns>
        public IList<SapConnectionInfoModel> GetConnections()
        {
            // Reuse the driver's discovery, which never reads user names or screen contents.
            return _host.Invoke(SapGuiNavigator.GetConnections);
        }

        /// <summary>
        /// Gets the identifier of the control that has the keyboard focus in a session's active window.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <returns>The focused control identifier, or null when nothing is focused or the session is gone.</returns>
        public string GetFocusedId(string sessionId)
        {
            return _host.Invoke(application =>
            {
                // Read ActiveWindow.GuiFocus, which covers both dynpro fields and ActiveX controls.
                var window = application.FindById(sessionId)?.GetNode("ActiveWindow");
                var focus = window is null ? null : TestNode(window, propertyName: "GuiFocus");

                return focus?.Id;
            });
        }

        /// <summary>
        /// Gets the identifier of a related object for keyboard tree navigation.
        /// </summary>
        /// <param name="id">The current object identifier.</param>
        /// <param name="direction">One of the <see cref="InspectorNavigation"/> constants.</param>
        /// <returns>The related identifier, or null when no such relative exists.</returns>
        public string GetRelativeId(string id, string direction)
        {
            return _host.Invoke(application =>
            {
                // Resolve the current object; navigation from a vanished object goes nowhere.
                var node = id == application.Id ? application : application.FindById(id);
                if (node is null)
                {
                    return null;
                }

                // Children directions read the node's own children; the others read its parent's.
                if (direction is InspectorNavigation.FirstChild or InspectorNavigation.LastChild)
                {
                    var children = node.GetChildren();
                    return children.Count == 0
                        ? null
                        : (direction == InspectorNavigation.FirstChild ? children[0] : children[^1]).Id;
                }

                var parent = TestNode(node, propertyName: "Parent");
                return direction == InspectorNavigation.Parent ? parent?.Id : GetSiblingId(parent, id, direction);
            });
        }

        /// <summary>
        /// Gets the root summary for the tree.
        /// </summary>
        /// <returns>The <c>GuiApplication</c> summary.</returns>
        public ElementSummary GetRoot()
        {
            // Attach (on first use) and summarize the application object.
            return _host.Invoke(NewSummary);
        }

        /// <summary>
        /// Gets everything the details pane shows for an object.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <returns>The snapshot, or null when the object no longer exists.</returns>
        public ElementSnapshot GetSnapshot(string id)
        {
            return _host.Invoke(application =>
            {
                // Resolve the object; screens change between hover and snapshot, so absence is normal.
                var node = id == application.Id ? application : application.FindById(id);
                if (node is null)
                {
                    return null;
                }

                // Read identity, geometry, properties, and the ancestor path in one operation.
                var summary = NewSummary(node);
                return new ElementSnapshot
                {
                    AncestorIds = GetAncestorIds(node),
                    Properties = PropertyCatalog.GetProperties(node, summary.Type),
                    Rectangle = SapGuiNavigator.GetRectangle(node),
                    SessionId = LocatorCandidates.GetSessionId(node.Id),
                    SubType = summary.Type == SapGuiTypes.Shell ? SapGuiNavigator.GetString(node, propertyName: "SubType") : string.Empty,
                    Summary = summary,
                    WindowId = LocatorCandidates.GetWindowId(node.Id)
                };
            });
        }

        /// <summary>
        /// Reads the status bar of a session's main window as display text.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <returns>Text such as <c>[S 00/344] Saved</c>, or an empty string when there is no status bar.</returns>
        public string GetStatusBar(string sessionId)
        {
            return _host.Invoke(application =>
            {
                // Read the main window status bar, the one SAP guarantees.
                var statusBar = application.FindById(sessionId)?.FindById("wnd[0]/sbar");
                if (statusBar is null)
                {
                    return string.Empty;
                }

                var type = SapGuiNavigator.GetString(statusBar, propertyName: "MessageType");
                var messageId = SapGuiNavigator.GetString(statusBar, propertyName: "MessageId");
                var number = SapGuiNavigator.GetString(statusBar, propertyName: "MessageNumber");

                return $"[{type} {messageId}/{number}] {SapGuiNavigator.GetString(statusBar, propertyName: "Text")}".Trim();
            });
        }

        /// <summary>
        /// Gets the virtual keys a session describes, for the virtual-key picker.
        /// </summary>
        /// <param name="sessionId">The session identifier.</param>
        /// <returns>The keys 0-99 that have a description, cached per session.</returns>
        public IList<VirtualKeyEntry> GetVirtualKeys(string sessionId)
        {
            return _host.Invoke(application =>
            {
                // Serve the cached list; descriptions do not change during a session.
                if (_virtualKeys.TryGetValue(sessionId, out var cached))
                {
                    return cached;
                }

                // Ask SAP GUI to describe each key; keys it rejects are not offered.
                var session = application.FindById(sessionId);
                if (session is null)
                {
                    return new List<VirtualKeyEntry>();
                }

                var keys = Enumerable
                    .Range(0, 100)
                    .Select(i => new VirtualKeyEntry(i, GetVirtualKeyDescription(session, i)))
                    .Where(i => i.Description.Length > 0)
                    .ToList();
                _virtualKeys[sessionId] = keys;

                return keys;
            });
        }

        /// <summary>
        /// Resolves the object under a screen point through <c>findByPosition</c> on the session that owns the window.
        /// </summary>
        /// <param name="point">The cursor sample, including the top-level window under the cursor.</param>
        /// <returns>The hit, or null when the point is not over a scriptable SAP GUI window.</returns>
        public HitResult HitTest(InspectorPoint point)
        {
            // Require the sample; the UI thread always supplies one.
            ArgumentNullException.ThrowIfNull(argument: point);

            return _host.Invoke(application =>
            {
                // Map the window to its session, rebuilding the cache once on a miss.
                var sessionId = GetSessionIdForWindow(application, point.WindowHandle);
                var session = sessionId is null ? null : application.FindById(sessionId);
                if (session is null)
                {
                    return null;
                }

                // Hit-test through the shared driver lookup, so the inspector and the driver agree on every point.
                var (node, innerObject) = SapGuiNavigator.FindByPosition(application, session, point.X, point.Y);

                return node is null ? null : NewHitResult(node, innerObject);
            });
        }

        /// <summary>
        /// Performs a quick action on a control.
        /// </summary>
        /// <param name="id">The control identifier.</param>
        /// <param name="actionId">One of the <see cref="ActionCatalog"/> action constants.</param>
        /// <param name="text">The action text; used by text actions only.</param>
        /// <exception cref="SapDriverException">The control is gone or does not support the action.</exception>
        public void InvokeAction(string id, string actionId, string text)
        {
            _host.Invoke(application =>
            {
                // Perform the action through the same member the driver uses.
                ActionCatalog.InvokeAction(GetRequiredNode(application, id), actionId, text);
            });
        }

        /// <summary>
        /// Calls a scripting method, or reads or writes a property, by name on an object.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <param name="invocation">The member kind, name, and typed arguments.</param>
        /// <returns>The formatted result.</returns>
        /// <exception cref="SapDriverException">The object is gone, or SAP GUI rejected the call.</exception>
        public string InvokeMember(string id, MemberInvocation invocation)
        {
            // Require the invocation and its member name before reaching SAP GUI.
            ArgumentNullException.ThrowIfNull(argument: invocation);
            if (string.IsNullOrWhiteSpace(invocation.Name))
            {
                throw new ArgumentException("The member name is required.", nameof(invocation));
            }

            return _host.Invoke(application =>
            {
                // Dispatch by kind; object-valued results are described rather than returned.
                var node = GetRequiredNode(application, id);
                var name = invocation.Name.Trim();
                var arguments = invocation.Arguments ?? [];

                switch (invocation.Kind)
                {
                    case MemberInvocation.SetProperty:
                        node.SetProperty(name, arguments.Length > 0 ? arguments[0] : null);
                        return "(property set)";
                    case MemberInvocation.GetProperty:
                        var value = node.GetProperty(name);
                        return value is not null && Marshal.IsComObject(value) ? ConvertToText(node.GetNode(name)) : ConvertToText(value);
                    default:
                        return ConvertToText(node.InvokeMethod(name, arguments));
                }
            });
        }

        /// <summary>
        /// Sends a SAP virtual key to a frame window.
        /// </summary>
        /// <param name="windowId">The frame window identifier.</param>
        /// <param name="key">The virtual key code.</param>
        /// <exception cref="SapDriverException">The window is gone or rejected the key.</exception>
        public void SendVirtualKey(string windowId, int key)
        {
            _host.Invoke(application =>
            {
                // Raise the key on the window, exactly as a recorded script does.
                GetRequiredNode(application, windowId).InvokeMethod("sendVKey", key);
            });
        }

        /// <summary>
        /// Shows or hides SAP GUI's own red frame around an object or an inner object such as a grid cell.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <param name="innerObject">The inner-object description from the hit test, or empty for the whole object.</param>
        /// <param name="isOn">True to show the frame; false to remove it.</param>
        /// <returns>True when SAP GUI accepted the request; false when the object is gone or does not support it.</returns>
        public bool SetVisualized(string id, string innerObject, bool isOn)
        {
            try
            {
                return _host.Invoke(application =>
                {
                    // Call Visualize; objects that vanished or do not support it are reported as false.
                    var node = application.FindById(id);
                    return node is not null && node.InvokeMethod("Visualize", isOn, innerObject ?? string.Empty) is true;
                });
            }
            catch (SapDriverException)
            {
                // Treat a refused frame as "not shown"; the overlay highlight still marks the control.
                return false;
            }
        }

        /// <summary>
        /// Maximizes a frame window.
        /// </summary>
        /// <param name="windowId">The frame window identifier.</param>
        /// <exception cref="SapDriverException">The window is gone or cannot be maximized.</exception>
        public void SetWindowMaximized(string windowId)
        {
            _host.Invoke(application =>
            {
                // Maximize through the scripting member, so SAP GUI handles its own layout.
                GetRequiredNode(application, windowId).InvokeMethod("maximize");
            });
        }

        // Formats an invocation result: scripting objects by type and id, Booleans in lowercase, null explicitly.
        private static string ConvertToText(object value)
        {
            return value switch
            {
                null => "(null)",
                ISapGuiNode node => $"{node.Type} {node.Id}",
                bool isSet => isSet ? "true" : "false",
                _ when Marshal.IsComObject(value) => "(scripting object)",
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }

        // Counts the matches of one candidate in the session and marks whether it identifies exactly the target.
        private static LocatorCandidate CountMatches(ISapGuiNode application, ISapGuiNode session, LocatorCandidate candidate)
        {
            // Evaluate through the driver's locator so the count is what a WebDriver client observes.
            var locator = new LocationStrategyModel { Using = candidate.Strategy, Value = candidate.Value };
            var matches = ElementLocator.FindElements(application, session, locator);

            candidate.MatchCount = matches.Count;
            candidate.IsUnique = matches.Count == 1 && string.Equals(matches[0].SapId, candidate.TargetId, StringComparison.Ordinal);

            return candidate;
        }

        // Walks the Parent chain up to the application and returns the identifiers root first.
        private static List<string> GetAncestorIds(ISapGuiNode node)
        {
            var ancestors = new List<string>();
            var parent = TestNode(node, propertyName: "Parent");

            // Stop at the application, whose parent is empty, and guard against unexpected cycles.
            while (parent is not null && ancestors.Count < DocumentObjectModelFactory.MaximumDepth + 8)
            {
                ancestors.Insert(0, parent.Id);
                parent = TestNode(parent, propertyName: "Parent");
            }

            return ancestors;
        }

        // Resolves an object that an action requires, failing with a stale reference when it is gone.
        private static ISapGuiNode GetRequiredNode(ISapGuiNode application, string id)
        {
            var node = string.IsNullOrEmpty(id) ? null : application.FindById(id);
            if (node is null)
            {
                var message = $"The object '{id}' no longer exists; select it again.";
                throw new SapDriverException(WebDriverErrorCodes.StaleElementReference, errorKind: null, statusCode: 404, message);
            }

            return node;
        }

        // Returns the identifier of the previous or next sibling of a child, or null at either end.
        private static string GetSiblingId(ISapGuiNode parent, string id, string direction)
        {
            // Without a parent there are no siblings.
            if (parent is null)
            {
                return null;
            }

            var siblings = parent.GetChildren();
            var index = siblings.ToList().FindIndex(i => string.Equals(i.Id, id, StringComparison.Ordinal));
            var target = direction == InspectorNavigation.PreviousSibling ? index - 1 : index + 1;

            return index < 0 || target < 0 || target >= siblings.Count ? null : siblings[target].Id;
        }

        // Asks SAP GUI for one virtual-key description; keys it rejects get an empty description.
        private static string GetVirtualKeyDescription(ISapGuiNode session, int key)
        {
            try
            {
                return Convert.ToString(session.InvokeMethod("GetVKeyDescription", key), CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
            }
            catch (Exception exception) when (exception is SapDriverException or COMException or ArgumentException)
            {
                return string.Empty;
            }
        }

        // Builds a hit result with summary and geometry for an object.
        private static HitResult NewHitResult(ISapGuiNode node, string innerObject)
        {
            return new HitResult
            {
                Id = node.Id,
                InnerObject = innerObject ?? string.Empty,
                Rectangle = SapGuiNavigator.GetRectangle(node),
                Summary = NewSummary(node)
            };
        }

        // Summarizes an object for the tree and the details header; password text is never read.
        private static ElementSummary NewSummary(ISapGuiNode node)
        {
            // Connections show their SAP Logon description; other objects show their text.
            var type = node.Type;
            var isStructural = type is "GuiApplication" or "GuiConnection" or SapGuiTypes.Session;
            var text = type switch
            {
                "GuiConnection" => SapGuiNavigator.GetString(node, propertyName: "Description"),
                SapGuiTypes.PasswordField or SapGuiTypes.Session or "GuiApplication" => string.Empty,
                _ => SapGuiNavigator.GetString(node, propertyName: "Text")
            };

            return new ElementSummary
            {
                Id = node.Id,
                IsContainer = isStructural || SapGuiNavigator.GetBoolean(node, propertyName: "ContainerType"),
                Name = SapGuiNavigator.GetString(node, propertyName: "Name"),
                Text = text,
                Type = type
            };
        }

        // Reads an object-valued property leniently; absent or scalar values yield null.
        private static ISapGuiNode TestNode(ISapGuiNode node, string propertyName)
        {
            return SapGuiNavigator.TestProperty(node, propertyName, out var value) && value is not null
                ? node.GetNode(propertyName)
                : null;
        }

        // Maps a top-level window handle to its session, rebuilding the handle cache on a miss at most once per interval.
        // Runs inside a scripting operation; the cache is owned by the dispatcher thread.
        private string GetSessionIdForWindow(ISapGuiNode application, long windowHandle)
        {
            // Serve the cache; handles stay stable for a window's lifetime.
            var handle = windowHandle.ToString("X", CultureInfo.InvariantCulture);
            if (_windowSessions.TryGetValue(handle, out var sessionId))
            {
                return sessionId;
            }

            // Rebuild only when the last rebuild is older than the interval, so non-SAP windows stay cheap.
            var now = _clock();
            if (_windowRefreshTime != long.MinValue && now - _windowRefreshTime < WindowRefreshMilliseconds)
            {
                return null;
            }

            _windowRefreshTime = now;
            _windowSessions.Clear();

            foreach (var session in SapGuiNavigator.GetConnections(application).SelectMany(i => i.Sessions))
            {
                var sessionNode = application.FindById(session.Id);
                foreach (var window in sessionNode?.GetChildren() ?? [])
                {
                    var windowKey = SapGuiNavigator.GetHandle(window);
                    if (windowKey.Length > 0)
                    {
                        _windowSessions[windowKey] = session.Id;
                    }
                }
            }

            return _windowSessions.TryGetValue(handle, out sessionId) ? sessionId : null;
        }
        #endregion

    }
}
