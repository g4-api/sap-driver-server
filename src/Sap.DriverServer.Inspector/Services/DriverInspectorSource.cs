using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Inspects SAP GUI through a WebDriver session of a SAP driver server, which can run on another machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree comes from the driver's page source, cached until an action, a refresh, or a hit on an unknown control
    /// invalidates it. Element references are cached per SAP identifier, because the driver issues one stable
    /// reference per control. Geometry, attributes, hit tests, locator checks, and actions go through the driver's
    /// W3C and SAP endpoints, so everything the inspector shows is what a WebDriver client observes.
    /// </para>
    /// <para>
    /// The driver offers no generic member calls, no focus query, and no SAP frame, so <see cref="Features"/> turns
    /// those off. The desktop overlay is on only for a driver on this machine; screen coordinates of a remote driver
    /// belong to another desktop. Disposing the source deletes the WebDriver session when the source created it;
    /// attached sessions stay open.
    /// </para>
    /// </remarks>
    public sealed class DriverInspectorSource : IInspectorSource
    {
        #region *** Constants    ***
        // Bounds how long disposal waits for the driver to delete an owned session.
        private static readonly TimeSpan DeleteTimeout = TimeSpan.FromSeconds(3);

        // Maps the driver attribute names read for "all properties" to the SAP property names the grid shows.
        private static readonly (string Attribute, string Name)[] ExtendedAttributes =
        [
            ("subType", "SubType"), ("containerType", "ContainerType"), ("defaultTooltip", "DefaultTooltip"),
            ("iconName", "IconName"), ("modified", "Modified"), ("highlighted", "Highlighted"), ("required", "Required"),
            ("numerical", "Numerical"), ("maxLength", "MaxLength"), ("key", "Key"), ("value", "Value"),
            ("selected", "Selected"), ("handle", "Handle")
        ];

        // Lists the hostnames that identify a driver on this machine, whose coordinates match this desktop.
        private static readonly HashSet<string> LocalHosts = new(StringComparer.OrdinalIgnoreCase) { "localhost", "127.0.0.1", "::1", "[::1]" };

        // Spaces refreshes triggered by misses (unknown controls or windows), so hovering over a changing screen stays cheap.
        private static readonly TimeSpan MissRefreshInterval = TimeSpan.FromSeconds(1);
        #endregion

        #region *** Fields       ***
        // Calls the driver endpoints; owned and disposed by this source.
        private readonly SapDriverClient _client;

        // Records whether this source created the WebDriver session and therefore deletes it.
        private readonly bool _isOwner;

        // Caches the element reference the driver issued for each SAP identifier.
        private readonly ConcurrentDictionary<string, string> _references = new(StringComparer.Ordinal);

        // Serializes page-source refreshes so concurrent misses load the source once.
        private readonly SemaphoreSlim _sourceLock = new(1, 1);

        // Serializes window-handle refreshes so concurrent hover samples read the handles once.
        private readonly SemaphoreSlim _windowLock = new(1, 1);

        // Records whether the session was already deleted, so disposal does not delete it twice.
        private bool _isDeleted;

        // Holds the parsed page source and its identifier index; replaced as a whole on refresh, null when stale.
        private SourceIndex _source;

        // Records when the source was last loaded, in tick-count milliseconds, for the miss refresh interval.
        private long _sourceTime = long.MinValue;

        // Maps hexadecimal window handles to window identifiers; replaced as a whole on refresh.
        private Dictionary<string, string> _windowHandles = new(StringComparer.OrdinalIgnoreCase);

        // Records when the window handles were last read, in tick-count milliseconds.
        private long _windowTime = long.MinValue;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="DriverInspectorSource"/> class for an existing WebDriver session.
        /// </summary>
        /// <param name="client">The driver client; the source takes ownership and disposes it.</param>
        /// <param name="sessionId">The WebDriver session identifier.</param>
        /// <param name="isOwner">True when the inspector created the session and deletes it on disconnect.</param>
        public DriverInspectorSource(SapDriverClient client, string sessionId, bool isOwner)
        {
            // Assert the client and the session; the source has no way to create either itself.
            AssertArguments(client, sessionId);

            // Keep the session and its ownership.
            _client = client;
            _isOwner = isOwner;
            SessionId = sessionId;

            // Allow the desktop overlay only when the driver shares this desktop.
            var isLocalDriver = LocalHosts.Contains(client.BaseAddress.Host);
            Features = new SourceFeatures
            {
                CanFollowFocus = false,
                CanInvokeMember = false,
                CanVisualize = false,
                HasDesktopOverlay = isLocalDriver,
                HasScreenCanvas = true
            };
            DisplayName = $"Session {sessionId[..Math.Min(8, sessionId.Length)]} @ {client.BaseAddress.Authority}";

            return;

            // Requires the driver client and a session identifier before the source derives its features from them.
            // The helper reads its parameters only and reports an invalid one through its argument name.
            static void AssertArguments(SapDriverClient client, string sessionId)
            {
                // Require the client and a non-blank WebDriver session identifier.
                ArgumentNullException.ThrowIfNull(argument: client);
                ArgumentException.ThrowIfNullOrWhiteSpace(argument: sessionId);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public string DisplayName { get; }

        /// <inheritdoc />
        public SourceFeatures Features { get; }

        /// <summary>
        /// Gets a value indicating whether the inspector created the session and deletes it on disconnect.
        /// </summary>
        public bool IsOwner => _isOwner;

        /// <summary>
        /// Gets the WebDriver session identifier.
        /// </summary>
        public string SessionId { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Deletes the WebDriver session when the inspector created it, then releases the client.
        /// </summary>
        /// <returns>A task that completes when the session is deleted and the client released.</returns>
        public async Task DisconnectAsync()
        {
            try
            {
                // Delete only owned sessions; attached sessions belong to someone else.
                if (_isOwner && !_isDeleted)
                {
                    _isDeleted = true;
                    await _client.DeleteSessionAsync(SessionId).ConfigureAwait(false);
                }
            }
            finally
            {
                // Release the HTTP client even when the driver is gone.
                Dispose();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Delete an owned session on a best-effort basis with a short budget, so closing the inspector stays quick.
            if (_isOwner && !_isDeleted)
            {
                _isDeleted = true;
                try
                {
                    Task.Run(() => _client.DeleteSessionAsync(SessionId)).Wait(DeleteTimeout);
                }
                catch (AggregateException)
                {
                    // The driver is already gone or refused; the session ends with the driver either way.
                }
            }

            _client.Dispose();
            _sourceLock.Dispose();
            _windowLock.Dispose();
        }

        /// <inheritdoc />
        public async Task<IList<HitResult>> FindByLocatorAsync(string sessionId, LocationStrategyModel locator)
        {
            // Evaluate through the driver and resolve each match's identity and geometry, bounded for the overlay pool.
            var references = await _client.FindElementsAsync(SessionId, locator).ConfigureAwait(false);
            var source = await GetSourceAsync().ConfigureAwait(false);
            var hits = new List<HitResult>();

            foreach (var reference in references.Take(InspectorService.MaximumLocatorMatches))
            {
                var id = await _client.GetAttributeAsync(SessionId, reference, name: "id").ConfigureAwait(false);
                var rect = await _client.GetRectAsync(SessionId, reference).ConfigureAwait(false);
                _references[id] = reference;

                hits.Add(new HitResult
                {
                    Id = id,
                    Rectangle = ConvertToRectangle(rect),
                    Summary = source.TryGetSummary(id) ?? NewFallbackSummary(id, type: string.Empty)
                });
            }

            return hits;
        }

        /// <inheritdoc />
        public async Task<IList<LocatorCandidate>> GenerateLocatorsAsync(string id)
        {
            // Build the shared candidates from the page-source attributes of the control.
            var element = await FindSourceElementAsync(id).ConfigureAwait(false);
            if (element is null)
            {
                return [];
            }

            var type = GetType(element);
            var candidates = LocatorCandidates.NewCandidates(id, type, getAttribute: i => (string)element.Attribute(i) ?? string.Empty);
            if (candidates.Count == 0)
            {
                return candidates;
            }

            // Count each candidate's matches through the driver; a unique match must be the target's own reference.
            var target = await GetReferenceAsync(id, type).ConfigureAwait(false);
            foreach (var candidate in candidates)
            {
                await CountMatchesAsync(candidate, target).ConfigureAwait(false);
            }

            return candidates;
        }

        /// <inheritdoc />
        public IList<QuickAction> GetActions(string type, string subType)
        {
            // Offer the driver-mapped actions; the driver has no focus endpoint.
            return [.. ActionCatalog.GetActions(type, subType).Where(i => i.Id != ActionCatalog.Focus)];
        }

        /// <inheritdoc />
        public async Task<IList<ElementSummary>> GetChildrenAsync(string id)
        {
            // Read the children from the cached page source.
            var element = await FindSourceElementAsync(id).ConfigureAwait(false);

            return element is null ? [] : [.. element.Elements().Select(NewSummary)];
        }

        /// <inheritdoc />
        public Task<string> GetFocusedIdAsync(string sessionId)
        {
            // The driver has no endpoint that reports the focused control.
            throw new NotSupportedException("Follow focus is available in local mode only.");
        }

        /// <inheritdoc />
        public async Task<string> GetRelativeIdAsync(string id, string direction)
        {
            // Navigate the page-source tree, which mirrors the scripting tree.
            var element = await FindSourceElementAsync(id).ConfigureAwait(false);
            if (element is null)
            {
                return null;
            }

            var relative = direction switch
            {
                InspectorNavigation.FirstChild => element.Elements().FirstOrDefault(),
                InspectorNavigation.LastChild => element.Elements().LastOrDefault(),
                InspectorNavigation.NextSibling => element.ElementsAfterSelf().FirstOrDefault(),
                InspectorNavigation.PreviousSibling => element.ElementsBeforeSelf().LastOrDefault(),
                _ => element.Parent
            };

            return relative is null ? null : (string)relative.Attribute("id");
        }

        /// <inheritdoc />
        public async Task<ElementSummary> GetRootAsync()
        {
            // Reload the page source; the root is the attached SAP session.
            var source = await RefreshSourceAsync().ConfigureAwait(false);

            return NewSummary(source.Root);
        }

        /// <inheritdoc />
        public async Task<ScreenCapture> GetScreenCaptureAsync()
        {
            // Capture the current window, then find which window that is and where it is on the screen.
            var image = await _client.GetScreenshotAsync(SessionId).ConfigureAwait(false);
            var handle = await _client.GetWindowHandleAsync(SessionId).ConfigureAwait(false);
            var windows = await GetWindowHandlesAsync(isForced: true).ConfigureAwait(false);
            var source = await GetSourceAsync().ConfigureAwait(false);

            // Fall back to the main window when the current window has no handle.
            var windowId = windows.TryGetValue(handle, out var match) ? match : $"{source.SessionPath}/wnd[0]";
            var element = source.TryGetElement(windowId);
            var reference = await GetReferenceAsync(windowId, element is null ? string.Empty : GetType(element)).ConfigureAwait(false);
            var rect = reference is null ? new ElementRectModel() : await _client.GetRectAsync(SessionId, reference).ConfigureAwait(false);

            return new ScreenCapture { Image = image, WindowId = windowId, WindowRect = rect };
        }

        /// <inheritdoc />
        public async Task<ElementSnapshot> GetSnapshotAsync(string id)
        {
            // Resolve the control in the page source and through the driver; screens change, so absence is normal.
            var element = await FindSourceElementAsync(id).ConfigureAwait(false);
            if (element is null)
            {
                return null;
            }

            var summary = NewSummary(element);
            var reference = await GetReferenceAsync(id, summary.Type).ConfigureAwait(false);
            if (reference is null)
            {
                return null;
            }

            // Read geometry and the extended attributes; the page source already holds the basic ones.
            var rect = await _client.GetRectAsync(SessionId, reference).ConfigureAwait(false);
            var properties = NewBasicProperties(element, summary, rect);
            properties.AddRange(await ReadExtendedPropertiesAsync(reference, summary.Type).ConfigureAwait(false));

            var source = await GetSourceAsync().ConfigureAwait(false);
            return new ElementSnapshot
            {
                AncestorIds = [.. element.Ancestors().Reverse().Select(i => (string)i.Attribute("id"))],
                Properties = properties,
                Rectangle = ConvertToRectangle(rect),
                SessionId = source.SessionPath,
                SubType = (string)element.Attribute("subType") ?? string.Empty,
                Summary = summary,
                WindowId = LocatorCandidates.GetWindowId(id)
            };
        }

        /// <inheritdoc />
        public async Task<string> GetStatusBarAsync(string sessionId)
        {
            // Format the status bar like local mode: [type id/number] text.
            var statusBar = await _client.GetStatusBarAsync(SessionId).ConfigureAwait(false);
            var text = $"[{statusBar.MessageType} {statusBar.MessageId}/{statusBar.MessageNumber}] {statusBar.Text}".Trim();

            return statusBar.MessageType is null && string.IsNullOrEmpty(statusBar.Text) ? string.Empty : text;
        }

        /// <inheritdoc />
        public Task<IList<VirtualKeyEntry>> GetVirtualKeysAsync(string sessionId)
        {
            // The driver cannot describe keys per screen, so offer the standard list.
            return Task.FromResult(VirtualKeyCatalog.GetStandardKeys());
        }

        /// <inheritdoc />
        public async Task<HitResult> HitTestAsync(InspectorPoint point)
        {
            // Require the sample; the window and canvas always supply one.
            ArgumentNullException.ThrowIfNull(argument: point);

            // Desktop samples count only over this session's windows on this machine; canvas samples pass handle 0.
            if (point.WindowHandle != 0 && !await TestSessionWindowAsync(point.WindowHandle).ConfigureAwait(false))
            {
                return null;
            }

            // Hit-test through the driver and keep the reference it issued.
            var elementAt = await _client.FindElementAtAsync(SessionId, point.X, point.Y).ConfigureAwait(false);
            if (elementAt is null)
            {
                return null;
            }

            _references[elementAt.SapId] = elementAt.Element;
            var source = await GetSourceForAsync(elementAt.SapId).ConfigureAwait(false);

            return new HitResult
            {
                Id = elementAt.SapId,
                InnerObject = elementAt.InnerObject ?? string.Empty,
                Rectangle = ConvertToRectangle(elementAt.Rect),
                Summary = source.TryGetSummary(elementAt.SapId) ?? NewFallbackSummary(elementAt.SapId, elementAt.Type)
            };
        }

        /// <inheritdoc />
        public async Task InvokeActionAsync(string id, string actionId, string text)
        {
            // Resolve the control's reference; a vanished control is a stale reference.
            var element = await FindSourceElementAsync(id).ConfigureAwait(false);
            var reference = await GetReferenceAsync(id, element is null ? string.Empty : GetType(element)).ConfigureAwait(false)
                ?? throw NewStaleException(id);

            // Perform the matching W3C command, then mark the page source stale because the screen may have changed.
            var action = actionId switch
            {
                ActionCatalog.Click => _client.ClickAsync(SessionId, reference),
                ActionCatalog.SetText => _client.SetValueAsync(SessionId, reference, text),
                ActionCatalog.Clear => _client.ClearAsync(SessionId, reference),
                _ => throw new NotSupportedException($"The action '{actionId}' is not available in a driver session.")
            };
            await action.ConfigureAwait(false);
            _source = null;
        }

        /// <inheritdoc />
        public Task<string> InvokeMemberAsync(string id, MemberInvocation invocation)
        {
            // The driver deliberately refuses arbitrary scripting calls.
            throw new NotSupportedException("Invoke member is available in local mode only; the driver does not run arbitrary scripting calls.");
        }

        /// <inheritdoc />
        public async Task SendVirtualKeyAsync(string windowId, int key)
        {
            // Address the window by its SAP name, then mark the page source stale.
            var virtualKey = new SapVirtualKeyModel { VirtualKey = key, Window = GetWindowName(windowId) };
            await _client.SendVirtualKeyAsync(SessionId, virtualKey).ConfigureAwait(false);
            _source = null;
        }

        /// <inheritdoc />
        public Task<bool> SetVisualizedAsync(string id, string innerObject, bool isOn)
        {
            // The driver has no endpoint for SAP GUI's own frame; the inspector frames controls itself.
            return Task.FromResult(false);
        }

        /// <inheritdoc />
        public async Task SetWindowMaximizedAsync(string windowId)
        {
            // Make the window current, because the driver maximizes the current window.
            await _client.SwitchToWindowAsync(SessionId, GetWindowName(windowId)).ConfigureAwait(false);
            await _client.MaximizeWindowAsync(SessionId).ConfigureAwait(false);
        }

        // Converts a W3C rectangle to the edge-based rectangle the highlighter uses.
        private static RectangleModel ConvertToRectangle(ElementRectModel rect)
        {
            return rect is null ? new RectangleModel() : new RectangleModel
            {
                Bottom = rect.Y + rect.Height,
                Left = rect.X,
                Right = rect.X + rect.Width,
                Top = rect.Y
            };
        }

        // Gets the SAP type of a page-source element; the type attribute is authoritative, the element name a fallback.
        private static string GetType(XElement element)
        {
            return (string)element.Attribute("type") ?? element.Name.LocalName;
        }

        // Gets the SAP window name, such as wnd[1], from a window identifier; empty addresses the current window.
        private static string GetWindowName(string windowId)
        {
            // Take the last identifier segment; identifiers without one address the current window.
            var index = windowId?.LastIndexOf('/') ?? -1;
            return index < 0 ? string.Empty : windowId[(index + 1)..];
        }

        // Builds the basic property rows from the page source and the rectangle, masking password text.
        private static List<PropertyEntry> NewBasicProperties(XElement element, ElementSummary summary, ElementRectModel rect)
        {
            // Show the page-source attributes, then the geometry, in the local catalog's order.
            var isPassword = summary.Type == SapGuiTypes.PasswordField;
            var rows = new List<(string Name, string Value)>
            {
                ("Id", summary.Id),
                ("Type", summary.Type),
                ("Name", summary.Name),
                ("Text", isPassword ? PropertyCatalog.MaskedValue : summary.Text),
                ("Tooltip", (string)element.Attribute("tooltip") ?? string.Empty),
                ("Changeable", (string)element.Attribute("changeable") ?? string.Empty),
                ("ScreenLeft", rect.X.ToString(CultureInfo.InvariantCulture)),
                ("ScreenTop", rect.Y.ToString(CultureInfo.InvariantCulture)),
                ("Width", rect.Width.ToString(CultureInfo.InvariantCulture)),
                ("Height", rect.Height.ToString(CultureInfo.InvariantCulture))
            };

            return [.. rows.Select(i => new PropertyEntry { IsBasic = true, Name = i.Name, Value = i.Value })];
        }

        // Builds the stale reference failure for a control that no longer exists.
        private static SapDriverException NewStaleException(string id)
        {
            // Use the same wording as local mode, so both sources ask the user to select again.
            var message = $"The object '{id}' no longer exists; select it again.";
            return new SapDriverException(WebDriverErrorCodes.StaleElementReference, errorKind: null, statusCode: 404, message);
        }

        // Builds a summary for a control the page source does not hold yet, from its identifier and type.
        private static ElementSummary NewFallbackSummary(string id, string type)
        {
            // Use the last identifier segment as the name; the text stays empty because it was never read.
            var index = id?.LastIndexOf('/') ?? -1;
            return new ElementSummary { Id = id, Name = index < 0 ? id : id[(index + 1)..], Text = string.Empty, Type = type };
        }

        // Summarizes a page-source element for the tree and the details header.
        private static ElementSummary NewSummary(XElement element)
        {
            return new ElementSummary
            {
                Id = (string)element.Attribute("id") ?? string.Empty,
                IsContainer = element.HasElements,
                Name = (string)element.Attribute("name") ?? string.Empty,
                Text = (string)element.Attribute("text") ?? string.Empty,
                Type = GetType(element)
            };
        }

        // Counts one candidate's matches through the driver and marks whether it identifies exactly the target.
        private async Task CountMatchesAsync(LocatorCandidate candidate, string target)
        {
            try
            {
                // Compare references: the driver issues one stable reference per control.
                var locator = new LocationStrategyModel { Using = candidate.Strategy, Value = candidate.Value };
                var matches = await _client.FindElementsAsync(SessionId, locator).ConfigureAwait(false);

                candidate.MatchCount = matches.Count;
                candidate.IsUnique = matches.Count == 1 && target is not null && matches[0] == target;
            }
            catch (SapDriverException)
            {
                // Report a candidate the driver rejects as matching nothing.
                candidate.MatchCount = 0;
                candidate.IsUnique = false;
            }
        }

        // Finds a page-source element by identifier, reloading the source once when the identifier is unknown.
        private async Task<XElement> FindSourceElementAsync(string id)
        {
            // Get a source that knows the identifier when possible, then look the element up.
            var source = await GetSourceForAsync(id).ConfigureAwait(false);
            return source.TryGetElement(id);
        }

        // Gets the element reference for a control, finding it through its absolute-id XPath on first use.
        private async Task<string> GetReferenceAsync(string id, string type)
        {
            // Serve the cache; the driver keeps one reference per control for the session's lifetime.
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            if (_references.TryGetValue(id, out var cached))
            {
                return cached;
            }

            // Resolve through findById; the wildcard element name works when the type is unknown.
            var xpath = string.IsNullOrEmpty(type) ? $"//*[@id={ElementLocator.ConvertToXpathLiteral(id)}]" : LocatorCandidates.NewIdXpath(id, type);
            var locator = new LocationStrategyModel { Using = LocationStrategyModel.Xpath, Value = xpath };
            var reference = await _client.FindElementAsync(SessionId, locator).ConfigureAwait(false);
            if (reference is not null)
            {
                _references[id] = reference;
            }

            return reference;
        }

        // Gets the cached page source, loading it when it is missing or stale.
        private async Task<SourceIndex> GetSourceAsync()
        {
            return _source ?? await RefreshSourceAsync().ConfigureAwait(false);
        }

        // Gets a page source that knows an identifier, reloading at most once per interval when it does not.
        private async Task<SourceIndex> GetSourceForAsync(string id)
        {
            // Serve the cache when it knows the identifier.
            var source = await GetSourceAsync().ConfigureAwait(false);
            if (source.TryGetElement(id) is not null)
            {
                return source;
            }

            // Reload for an unknown identifier, throttled so hovering over a changed screen does not reload repeatedly.
            var isDue = _sourceTime == long.MinValue || Environment.TickCount64 - _sourceTime >= MissRefreshInterval.TotalMilliseconds;
            return isDue ? await RefreshSourceAsync().ConfigureAwait(false) : source;
        }

        // Reads the session's windows and their handles, at most once per interval unless forced.
        private async Task<Dictionary<string, string>> GetWindowHandlesAsync(bool isForced)
        {
            // Serve the cache within the interval.
            var isDue = _windowTime == long.MinValue || Environment.TickCount64 - _windowTime >= MissRefreshInterval.TotalMilliseconds;
            if (!isForced && !isDue)
            {
                return _windowHandles;
            }

            await _windowLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Read each top-level window's handle attribute through the driver; actions invalidate the source.
                var source = await GetSourceAsync().ConfigureAwait(false);
                var handles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var window in source.Root.Elements())
                {
                    var windowId = (string)window.Attribute("id");
                    var reference = await GetReferenceAsync(windowId, GetType(window)).ConfigureAwait(false);
                    var handle = reference is null ? string.Empty : await _client.GetAttributeAsync(SessionId, reference, name: "handle").ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(handle))
                    {
                        handles[handle] = windowId;
                    }
                }

                _windowHandles = handles;
                _windowTime = Environment.TickCount64;

                return handles;
            }
            finally
            {
                _windowLock.Release();
            }
        }

        // Reads the extended attributes through the driver, skipping empty values; password values stay masked.
        private async Task<IList<PropertyEntry>> ReadExtendedPropertiesAsync(string reference, string type)
        {
            // Read the attributes concurrently; the driver serializes the SAP calls, the HTTP round trips overlap.
            var isPassword = type == SapGuiTypes.PasswordField;
            var reads = ExtendedAttributes
                .Select(async i => (i.Name, Value: isPassword && i.Attribute == "value"
                    ? PropertyCatalog.MaskedValue
                    : await _client.GetAttributeAsync(SessionId, reference, i.Attribute).ConfigureAwait(false)))
                .ToList();
            var values = await Task.WhenAll(reads).ConfigureAwait(false);

            return [.. values.Where(i => !string.IsNullOrEmpty(i.Value)).Select(i => new PropertyEntry { Name = i.Name, Value = i.Value })];
        }

        // Loads and indexes the page source; concurrent callers share one load.
        private async Task<SourceIndex> RefreshSourceAsync()
        {
            var startTime = Environment.TickCount64;
            await _sourceLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Reuse a load that finished while this caller waited.
                if (_source is not null && _sourceTime >= startTime)
                {
                    return _source;
                }

                var xml = await _client.GetSourceAsync(SessionId).ConfigureAwait(false);
                _source = new SourceIndex(XDocument.Parse(xml).Root);
                _sourceTime = Environment.TickCount64;

                return _source;
            }
            finally
            {
                _sourceLock.Release();
            }
        }

        // Tests whether a top-level window handle belongs to the inspected session; refreshes the handles on a miss.
        private async Task<bool> TestSessionWindowAsync(long windowHandle)
        {
            // Desktop samples are meaningless for a driver on another machine.
            if (!Features.HasDesktopOverlay)
            {
                return false;
            }

            var handle = windowHandle.ToString("X", CultureInfo.InvariantCulture);
            if (_windowHandles.ContainsKey(handle))
            {
                return true;
            }

            var handles = await GetWindowHandlesAsync(isForced: false).ConfigureAwait(false);
            return handles.ContainsKey(handle);
        }
        #endregion

        #region *** Nested Types ***
        // Holds one parsed page source and an identifier index over it; immutable once built.
        private sealed class SourceIndex
        {
            // Maps SAP identifiers to their page-source elements.
            private readonly Dictionary<string, XElement> _elements;

            public SourceIndex(XElement root)
            {
                // Index every element that carries an identifier; the root is the attached session.
                Root = root ?? throw new ArgumentNullException(nameof(root));
                _elements = root
                    .DescendantsAndSelf()
                    .Where(i => i.Attribute("id") is not null)
                    .GroupBy(i => (string)i.Attribute("id"), StringComparer.Ordinal)
                    .ToDictionary(i => i.Key, i => i.First(), StringComparer.Ordinal);
            }

            // Holds the root element, which is the attached SAP session.
            public XElement Root { get; }

            // Gets the attached session's identifier, such as /app/con[0]/ses[0]; the tree's root and every snapshot's session.
            public string SessionPath => (string)Root.Attribute("id") ?? string.Empty;

            // Finds an element by SAP identifier; null when the source does not hold it (yet).
            public XElement TryGetElement(string id)
            {
                return id is not null && _elements.TryGetValue(id, out var element) ? element : null;
            }

            // Summarizes an element by SAP identifier; null when the source does not hold it, so callers can fall back.
            public ElementSummary TryGetSummary(string id)
            {
                var element = TryGetElement(id);
                return element is null ? null : NewSummary(element);
            }
        }
        #endregion
    }
}
