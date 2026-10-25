using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Provides everything the inspector window reads from or does to SAP GUI, independent of how SAP GUI is reached.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two sources exist: <see cref="LocalInspectorSource"/> talks to SAP GUI on this machine over COM and needs no
    /// driver; <see cref="DriverInspectorSource"/> works through a WebDriver session of a SAP driver server, which can
    /// run on another machine. The window switches between them without knowing which one is active.
    /// </para>
    /// <para>
    /// Every asynchronous member runs its SAP or HTTP work off the UI thread. Members a source cannot support are
    /// reported through <see cref="Features"/>; calling them anyway fails with <see cref="NotSupportedException"/>.
    /// Disposing a driver source deletes the WebDriver session it created; the local source owns nothing.
    /// </para>
    /// </remarks>
    public interface IInspectorSource : IDisposable
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the name shown in the source selector, such as <c>Local (COM)</c>.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Gets the features this source supports.
        /// </summary>
        SourceFeatures Features { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Finds the objects a driver locator matches in a session, for the locator tester.
        /// </summary>
        /// <param name="sessionId">The SAP session identifier.</param>
        /// <param name="locator">The locator, evaluated exactly as the driver evaluates it.</param>
        /// <returns>Up to <see cref="InspectorService.MaximumLocatorMatches"/> matches with their geometry.</returns>
        Task<IList<HitResult>> FindByLocatorAsync(string sessionId, LocationStrategyModel locator);

        /// <summary>
        /// Generates driver locators for an object and verifies how many objects each one matches.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <returns>The candidates, most specific last; empty above session level.</returns>
        Task<IList<LocatorCandidate>> GenerateLocatorsAsync(string id);

        /// <summary>
        /// Gets the quick actions this source can perform on a control type.
        /// </summary>
        /// <param name="type">The SAP type.</param>
        /// <param name="subType">The shell subtype, or empty.</param>
        /// <returns>The actions in button order.</returns>
        IList<QuickAction> GetActions(string type, string subType);

        /// <summary>
        /// Gets the child summaries of an object for the lazily expanded tree.
        /// </summary>
        /// <param name="id">The parent identifier.</param>
        /// <returns>The children in SAP order; empty when the parent is gone.</returns>
        Task<IList<ElementSummary>> GetChildrenAsync(string id);

        /// <summary>
        /// Gets the identifier of the control that has the keyboard focus in a session.
        /// </summary>
        /// <param name="sessionId">The SAP session identifier.</param>
        /// <returns>The focused control identifier, or null.</returns>
        Task<string> GetFocusedIdAsync(string sessionId);

        /// <summary>
        /// Gets the identifier of a related object for keyboard tree navigation.
        /// </summary>
        /// <param name="id">The current object identifier.</param>
        /// <param name="direction">One of the <see cref="InspectorNavigation"/> constants.</param>
        /// <returns>The related identifier, or null when no such relative exists.</returns>
        Task<string> GetRelativeIdAsync(string id, string direction);

        /// <summary>
        /// Gets the root summary for the tree.
        /// </summary>
        /// <returns>
        /// The application (local) or the session (driver) summary, or null when the source has nothing to show yet, such
        /// as a local source on a machine where SAP Logon is not running.
        /// </returns>
        Task<ElementSummary> GetRootAsync();

        /// <summary>
        /// Captures the SAP window the screen canvas shows.
        /// </summary>
        /// <returns>The screenshot and the window geometry.</returns>
        Task<ScreenCapture> GetScreenCaptureAsync();

        /// <summary>
        /// Gets everything the details pane shows for an object.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <returns>The snapshot, or null when the object no longer exists.</returns>
        Task<ElementSnapshot> GetSnapshotAsync(string id);

        /// <summary>
        /// Reads the status bar of a session's main window as display text.
        /// </summary>
        /// <param name="sessionId">The SAP session identifier.</param>
        /// <returns>Text such as <c>[S 00/344] Saved</c>, or an empty string.</returns>
        Task<string> GetStatusBarAsync(string sessionId);

        /// <summary>
        /// Gets the virtual keys offered by the virtual-key picker.
        /// </summary>
        /// <param name="sessionId">The SAP session identifier.</param>
        /// <returns>The keys with their descriptions.</returns>
        Task<IList<VirtualKeyEntry>> GetVirtualKeysAsync(string sessionId);

        /// <summary>
        /// Resolves the object under a screen point.
        /// </summary>
        /// <param name="point">The point in screen pixels; a zero window handle means "any window of the inspected session".</param>
        /// <returns>The hit, or null when no inspected control is under the point.</returns>
        Task<HitResult> HitTestAsync(InspectorPoint point);

        /// <summary>
        /// Performs a quick action on a control.
        /// </summary>
        /// <param name="id">The control identifier.</param>
        /// <param name="actionId">One of the <see cref="ActionCatalog"/> action constants.</param>
        /// <param name="text">The action text; used by text actions only.</param>
        /// <returns>A task that completes when SAP GUI performed the action.</returns>
        Task InvokeActionAsync(string id, string actionId, string text);

        /// <summary>
        /// Calls a scripting method, or reads or writes a property, by name on an object.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <param name="invocation">The member kind, name, and typed arguments.</param>
        /// <returns>The formatted result.</returns>
        Task<string> InvokeMemberAsync(string id, MemberInvocation invocation);

        /// <summary>
        /// Sends a SAP virtual key to a frame window.
        /// </summary>
        /// <param name="windowId">The frame window identifier.</param>
        /// <param name="key">The virtual key code.</param>
        /// <returns>A task that completes when the key was sent.</returns>
        Task SendVirtualKeyAsync(string windowId, int key);

        /// <summary>
        /// Shows or hides SAP GUI's own frame around an object or an inner object.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <param name="innerObject">The inner-object description, or empty.</param>
        /// <param name="isOn">True to show the frame; false to remove it.</param>
        /// <returns>True when SAP GUI accepted the request.</returns>
        Task<bool> SetVisualizedAsync(string id, string innerObject, bool isOn);

        /// <summary>
        /// Maximizes a frame window.
        /// </summary>
        /// <param name="windowId">The frame window identifier.</param>
        /// <returns>A task that completes when the window is maximized.</returns>
        Task SetWindowMaximizedAsync(string windowId);
        #endregion
    }
}
