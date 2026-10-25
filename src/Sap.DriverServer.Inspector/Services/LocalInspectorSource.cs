using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Models;
using Sap.DriverServer.Scripting;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Inspects SAP GUI on this machine directly over COM, without a driver server.
    /// </summary>
    /// <remarks>
    /// The source adapts the synchronous <see cref="InspectorService"/> to <see cref="IInspectorSource"/> by running each
    /// call on the thread pool; the service itself marshals every COM call to its STA dispatcher. It finds SAP Logon
    /// through the Windows running object table, so it needs no SAP registration and starts cleanly on machines
    /// without SAP GUI. Before it attaches, <see cref="GetRootAsync"/> checks the table for the <c>SAPGUI</c> entry
    /// and returns no root while it is absent, so a machine without SAP Logon is a normal state, not an exception.
    /// The source owns nothing; the application owns and disposes the scripting host.
    /// </remarks>
    public sealed class LocalInspectorSource : IInspectorSource
    {
        #region *** Constants    ***
        // Names the running object table entry SAP Logon registers while it runs.
        private const string RotEntryName = "SAPGUI";
        #endregion

        #region *** Fields       ***
        // Reports whether SAP Logon is running, without attaching and without throwing.
        private readonly Func<bool> _isSapGuiRunning;

        // Performs every COM call on the scripting host.
        private readonly InspectorService _service;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="LocalInspectorSource"/> class that looks for SAP Logon in the
        /// running object table of this Windows user.
        /// </summary>
        /// <param name="service">The COM inspection service.</param>
        public LocalInspectorSource(InspectorService service)
            : this(service, isSapGuiRunning: TestSapGuiRunning)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalInspectorSource"/> class with an explicit SAP Logon check.
        /// </summary>
        /// <param name="service">The COM inspection service.</param>
        /// <param name="isSapGuiRunning">Reports whether SAP Logon runs; it never attaches and never throws.</param>
        public LocalInspectorSource(InspectorService service, Func<bool> isSapGuiRunning)
        {
            // Require both collaborators; the source has no fallback for either.
            AssertArguments(service, isSapGuiRunning);

            _service = service;
            _isSapGuiRunning = isSapGuiRunning;

            return;

            // Requires the service and the check before the source stores them.
            static void AssertArguments(InspectorService service, Func<bool> isSapGuiRunning)
            {
                // Report a missing collaborator under its own name.
                ArgumentNullException.ThrowIfNull(argument: service);
                ArgumentNullException.ThrowIfNull(argument: isSapGuiRunning);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public string DisplayName => "Local (COM)";

        /// <inheritdoc />
        public SourceFeatures Features { get; } = new()
        {
            CanFollowFocus = true,
            CanInvokeMember = true,
            CanVisualize = true,
            HasDesktopOverlay = true,
            HasScreenCanvas = false
        };
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // The scripting host belongs to the application, so there is nothing to release here.
        }

        /// <inheritdoc />
        public Task<IList<HitResult>> FindByLocatorAsync(string sessionId, LocationStrategyModel locator)
        {
            return Task.Run(() => _service.FindByLocator(sessionId, locator));
        }

        /// <inheritdoc />
        public Task<IList<LocatorCandidate>> GenerateLocatorsAsync(string id)
        {
            return Task.Run(() => _service.GenerateLocators(id));
        }

        /// <inheritdoc />
        public IList<QuickAction> GetActions(string type, string subType)
        {
            // Offer the driver-mapped actions plus focus, which COM can set directly.
            return ActionCatalog.GetActions(type, subType);
        }

        /// <inheritdoc />
        public Task<IList<ElementSummary>> GetChildrenAsync(string id)
        {
            return Task.Run(() => _service.GetChildren(id));
        }

        /// <summary>
        /// Gets the open connections and sessions on this machine, used to prefill driver session options.
        /// </summary>
        /// <returns>The connections; identifiers and system metadata only.</returns>
        public Task<IList<SapConnectionInfoModel>> GetConnectionsAsync()
        {
            return Task.Run(_service.GetConnections);
        }

        /// <inheritdoc />
        public Task<string> GetFocusedIdAsync(string sessionId)
        {
            return Task.Run(() => _service.GetFocusedId(sessionId));
        }

        /// <inheritdoc />
        public Task<string> GetRelativeIdAsync(string id, string direction)
        {
            return Task.Run(() => _service.GetRelativeId(id, direction));
        }

        /// <inheritdoc />
        /// <remarks>
        /// Returns no root while SAP Logon is not running, without attaching, so the caller shows its waiting state and no
        /// exception is raised. The engine still reports a SAP Logon that exits between the check and the attach.
        /// </remarks>
        public Task<ElementSummary> GetRootAsync()
        {
            // Check for SAP Logon first; attaching to a missing SAP Logon is the only way it fails.
            return Task.Run(() => _isSapGuiRunning() ? _service.GetRoot() : null);
        }

        /// <inheritdoc />
        public Task<ScreenCapture> GetScreenCaptureAsync()
        {
            // Local mode highlights the live desktop instead of a screenshot.
            throw new NotSupportedException("The screen canvas is available in driver sessions only.");
        }

        /// <inheritdoc />
        public Task<ElementSnapshot> GetSnapshotAsync(string id)
        {
            return Task.Run(() => _service.GetSnapshot(id));
        }

        /// <inheritdoc />
        public Task<string> GetStatusBarAsync(string sessionId)
        {
            return Task.Run(() => _service.GetStatusBar(sessionId));
        }

        /// <inheritdoc />
        public Task<IList<VirtualKeyEntry>> GetVirtualKeysAsync(string sessionId)
        {
            // Ask SAP GUI for its own key descriptions, which match the current screen.
            return Task.Run(() => _service.GetVirtualKeys(sessionId));
        }

        /// <inheritdoc />
        public Task<HitResult> HitTestAsync(InspectorPoint point)
        {
            return Task.Run(() => _service.HitTest(point));
        }

        /// <inheritdoc />
        public Task InvokeActionAsync(string id, string actionId, string text)
        {
            return Task.Run(() => _service.InvokeAction(id, actionId, text));
        }

        /// <inheritdoc />
        public Task<string> InvokeMemberAsync(string id, MemberInvocation invocation)
        {
            return Task.Run(() => _service.InvokeMember(id, invocation));
        }

        /// <inheritdoc />
        public Task SendVirtualKeyAsync(string windowId, int key)
        {
            return Task.Run(() => _service.SendVirtualKey(windowId, key));
        }

        /// <inheritdoc />
        public Task<bool> SetVisualizedAsync(string id, string innerObject, bool isOn)
        {
            return Task.Run(() => _service.SetVisualized(id, innerObject, isOn));
        }

        /// <inheritdoc />
        public Task SetWindowMaximizedAsync(string windowId)
        {
            return Task.Run(() => _service.SetWindowMaximized(windowId));
        }

        /// <summary>
        /// Tests whether SAP Logon runs for this Windows user, by looking for its <c>SAPGUI</c> running object table entry.
        /// </summary>
        /// <returns>True when a live process registered the entry; false otherwise, including when COM is unusable.</returns>
        /// <remarks>
        /// The check enumerates the table only; it never requests the scripting engine, so it does not attach, and SAP
        /// GUI Scripting settings do not affect it. The table lists only entries of the same user and integrity level.
        /// </remarks>
        public static bool TestSapGuiRunning()
        {
            try
            {
                // Bind to the entry; entries of exited processes do not bind and count as absent.
                var entry = new RunningObjectTableReader().GetObject(RotEntryName);
                if (entry is null)
                {
                    return false;
                }

                // Release the probe's reference at once; the engine binds its own when it attaches.
                if (Marshal.IsComObject(entry))
                {
                    Marshal.ReleaseComObject(entry);
                }

                return true;
            }
            catch (COMException)
            {
                // An unreadable table, such as an access denial, leaves the attach to report the precise reason.
                return true;
            }
        }
        #endregion
    }
}
