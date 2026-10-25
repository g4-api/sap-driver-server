using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

using Sap.DriverServer.Domain;
using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Mvvm;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Inspector.Views;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.ViewModels
{
    /// <summary>
    /// Drives the inspector window: the source selector, live inspection, the tree, the screen canvas, the details pane,
    /// locators, and ad-hoc actions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The window works against one <see cref="IInspectorSource"/> at a time: local COM inspection, which needs no
    /// driver, or a driver session created through the Create session dialog. Switching sources resets the tree, the
    /// selection, and every highlight. Features a source lacks (follow focus, member invoker, SAP frame, desktop
    /// overlay, screen canvas) are hidden or skipped through <see cref="Features"/>.
    /// </para>
    /// <para>
    /// While local inspection has no SAP GUI to attach to, the window stays usable and retries every
    /// <see cref="LocalRetryTicks"/> cursor ticks; a missing SAP Logon is a normal state, not an error.
    /// </para>
    /// <para>
    /// Awaits here deliberately resume on the UI thread (<c>ConfigureAwait(true)</c>), because the continuations update
    /// bound collections and properties; this is the documented exception to the library-wide
    /// <c>ConfigureAwait(false)</c> rule. Selections carry a version number, so a slow snapshot for an old hover never
    /// overwrites a newer selection. Locators are generated only for deliberate selections.
    /// </para>
    /// </remarks>
    public sealed class MainViewModel : ObservableObject, IDisposable
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies how many cursor ticks pass between two attempts to attach to a local SAP GUI (about 3 seconds).
        /// </summary>
        public const int LocalRetryTicks = 20;

        // Describes why the locator list is empty while hovering.
        private const string PauseHint = "Pause live inspection (Shift+F5) or select a node to generate locators.";

        // Marks a selection made by clicking the screen canvas; deliberate, so locators are generated.
        private const string SourceCanvas = "canvas";

        // Marks a selection that followed the SAP GUI keyboard focus; deliberate, so locators are generated.
        private const string SourceFocus = "focus";

        // Marks a selection made by hovering; locators are skipped to keep hover responsive.
        private const string SourceHover = "hover";

        // Marks a selection made with the navigation commands; deliberate, so locators are generated.
        private const string SourceNavigation = "navigation";

        // Marks a reselection after an action changed the screen; deliberate, so locators are generated.
        private const string SourceRefresh = "refresh";

        // Marks a selection made in the tree; the tree already shows it, so it is not revealed again.
        private const string SourceTree = "tree";

        // Spaces focus polls to every third cursor tick, which keeps focus following responsive but cheap.
        private const int FocusPollInterval = 3;

        // Reports the neutral state of local mode in the status line while no SAP Logon runs on this machine.
        private const string WaitingStatusText = "Waiting for SAP GUI on this machine…";

        // Explains the neutral state of local mode when no SAP Logon runs on this machine.
        private const string WaitingText = "No SAP GUI on this machine yet. Local inspection starts on its own when SAP Logon runs; " +
            "use Create session to inspect through a driver.";
        #endregion

        #region *** Fields       ***
        // Draws the selection and locator-match overlays on this desktop.
        private readonly Highlighter _highlighter;

        // Converts cursor and canvas samples into hover selections.
        private readonly LiveInspector _live;

        // Holds the driver-free local source, which is always available in the selector.
        private readonly LocalInspectorSource _localSource;

        // Reads and saves the capability sets offered by the Create session dialog.
        private readonly CapabilitySetStore _setStore;

        // Backs ActionText.
        private string _actionText = string.Empty;

        // Records the focused control seen by the last focus poll, so unchanged focus does not reselect.
        private string _lastFocusId;

        // Records the last local attach failure shown, so retries do not repeat the same message.
        private string _lastLocalError;

        // Backs Header.
        private string _header = "Hover over a SAP GUI control";

        // Backs InvokeArguments.
        private string _invokeArguments = string.Empty;

        // Backs InvokeKind.
        private string _invokeKind = MemberInvocation.Method;

        // Backs InvokeName.
        private string _invokeName = string.Empty;

        // Backs InvokeResult.
        private string _invokeResult = string.Empty;

        // Guards focus polling against overlapping ticks.
        private bool _isFocusPolling;

        // Backs IsFollowFocus.
        private bool _isFollowFocus;

        // Backs IsHighlighterEnabled.
        private bool _isHighlighterEnabled = true;

        // Records whether local inspection is attached to SAP GUI; false until the first successful load.
        private bool _isLocalAttached;

        // Guards local attach attempts against overlapping ticks.
        private bool _isLocalRetrying;

        // Backs IsPaused.
        private bool _isPaused;

        // Backs LocatorHint.
        private string _locatorHint = PauseHint;

        // Counts cursor ticks since the last local attach attempt.
        private int _localRetryCount;

        // Backs PropertyFilter.
        private string _propertyFilter = string.Empty;

        // Backs SelectedId.
        private string _selectedId = string.Empty;

        // Backs SelectedVirtualKey.
        private VirtualKeyEntry _selectedVirtualKey;

        // Increments with every selection and source switch, so stale snapshots are discarded.
        private int _selectionVersion;

        // Backs ShowAllProperties.
        private bool _showAllProperties;

        // Holds the snapshot shown in the details pane, or null before the first selection.
        private ElementSnapshot _snapshot;

        // Backs SelectedSource; the source every operation goes to.
        private IInspectorSource _source;

        // Backs StatusText.
        private string _statusText = "Starting…";

        // Backs TestResult.
        private string _testResult = string.Empty;

        // Backs TestStrategy.
        private string _testStrategy = LocationStrategyModel.Xpath;

        // Backs TestValue.
        private string _testValue = string.Empty;

        // Counts cursor ticks for the focus poll interval.
        private int _tickCount;

        // Backs TreeHint.
        private string _treeHint = string.Empty;

        // Records the object and inner object SAP GUI currently frames with Visualize, so the frame can be removed.
        private (string Id, string InnerObject) _visualized;

        // Records the session whose virtual keys are loaded.
        private string _virtualKeySessionId;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel"/> class.
        /// </summary>
        /// <param name="localSource">The driver-free local source.</param>
        /// <param name="highlighter">The desktop overlay manager.</param>
        /// <param name="setStore">The saved capability sets for the Create session dialog.</param>
        public MainViewModel(LocalInspectorSource localSource, Highlighter highlighter, CapabilitySetStore setStore)
        {
            // Assert the collaborators before any state is created; the window cannot work without them.
            AssertArguments(localSource, highlighter, setStore);

            // Keep the collaborators and start with local inspection selected.
            _localSource = localSource;
            _highlighter = highlighter;
            _setStore = setStore;
            _source = localSource;
            Sources.Add(localSource);

            // Route hover results from the desktop and the canvas into the selection flow.
            _live = new LiveInspector(point => _source.HitTestAsync(point));
            _live.HitChanged += (_, hit) => _ = SelectAsync(hit.Id, hit.InnerObject, SourceHover);
            _live.ResolveFailed += (_, exception) => ShowError(exception);

            // Filter the property grid by the basic/all toggle and the filter text.
            PropertiesView = CollectionViewSource.GetDefaultView(Properties);
            PropertiesView.Filter = TestPropertyVisible;

            // Wire the commands; each one reports failures in the status bar.
            ClearLocatorTestCommand = new RelayCommand(_ => HideMatches());
            CopyCommand = new RelayCommand(value => SetClipboard($"{value}"), value => !string.IsNullOrEmpty($"{value}"));
            DisconnectCommand = new RelayCommand(_ => _ = RunAsync(DisconnectAsync), _ => _source is DriverInspectorSource);
            InvokeMemberCommand = new RelayCommand(_ => _ = RunAsync(InvokeMemberAsync), _ => TestHasSelection() && Features.CanInvokeMember);
            NavigateCommand = new RelayCommand(direction => _ = RunAsync(() => NavigateAsync($"{direction}")), _ => TestHasSelection());
            RefreshCommand = new RelayCommand(_ => _ = RunAsync(RefreshAsync));
            RefreshScreenCommand = new RelayCommand(_ => _ = RunAsync(RefreshScreenAsync), _ => Features.HasScreenCanvas);
            SetWindowMaximizedCommand = new RelayCommand(_ => _ = RunAsync(SetWindowMaximizedAsync), _ => TestHasWindow());
            ReadStatusBarCommand = new RelayCommand(_ => _ = RunAsync(ReadStatusBarAsync), _ => TestHasSession());
            SendVirtualKeyCommand = new RelayCommand(_ => _ = RunAsync(SendVirtualKeyAsync), _ => TestHasWindow() && SelectedVirtualKey is not null);
            TestLocatorCommand = new RelayCommand(_ => _ = RunAsync(TestLocatorAsync), _ => TestHasSession() && !string.IsNullOrWhiteSpace(TestValue));
            TogglePauseCommand = new RelayCommand(_ => IsPaused = !IsPaused);

            return;

            // Requires every collaborator before the constructor wires commands and events to them.
            // The helper reads its parameters only and reports a missing one through its argument name.
            static void AssertArguments(LocalInspectorSource localSource, Highlighter highlighter, CapabilitySetStore setStore)
            {
                // Require the local source, the overlay manager, and the capability set store.
                ArgumentNullException.ThrowIfNull(argument: localSource);
                ArgumentNullException.ThrowIfNull(argument: highlighter);
                ArgumentNullException.ThrowIfNull(argument: setStore);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the text used by text quick actions.
        /// </summary>
        public string ActionText
        {
            get => _actionText;
            set => SetProperty(ref _actionText, value);
        }

        /// <summary>
        /// Gets the command that removes locator-match highlights.
        /// </summary>
        public ICommand ClearLocatorTestCommand { get; }

        /// <summary>
        /// Gets the command that copies its parameter to the clipboard.
        /// </summary>
        public ICommand CopyCommand { get; }

        /// <summary>
        /// Gets the command that disconnects the selected driver session and returns to local inspection.
        /// </summary>
        public ICommand DisconnectCommand { get; }

        /// <summary>
        /// Gets the features of the selected source, which decide the visible sections and commands.
        /// </summary>
        public SourceFeatures Features => _source.Features;

        /// <summary>
        /// Gets the details header: type, name, and inner object.
        /// </summary>
        public string Header
        {
            get => _header;
            private set => SetProperty(ref _header, value);
        }

        /// <summary>
        /// Gets or sets the generic invoker's argument text.
        /// </summary>
        public string InvokeArguments
        {
            get => _invokeArguments;
            set => SetProperty(ref _invokeArguments, value);
        }

        /// <summary>
        /// Gets or sets the generic invoker's member kind.
        /// </summary>
        public string InvokeKind
        {
            get => _invokeKind;
            set => SetProperty(ref _invokeKind, value);
        }

        /// <summary>
        /// Gets the member kinds offered by the generic invoker.
        /// </summary>
        public IList<string> InvokeKinds { get; } = [MemberInvocation.Method, MemberInvocation.GetProperty, MemberInvocation.SetProperty];

        /// <summary>
        /// Gets the command that runs the generic invoker.
        /// </summary>
        public ICommand InvokeMemberCommand { get; }

        /// <summary>
        /// Gets or sets the generic invoker's member name.
        /// </summary>
        public string InvokeName
        {
            get => _invokeName;
            set => SetProperty(ref _invokeName, value);
        }

        /// <summary>
        /// Gets the generic invoker's last result.
        /// </summary>
        public string InvokeResult
        {
            get => _invokeResult;
            private set => SetProperty(ref _invokeResult, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether live inspection also follows the keyboard focus in SAP GUI.
        /// </summary>
        public bool IsFollowFocus
        {
            get => _isFollowFocus;
            set => SetProperty(ref _isFollowFocus, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the selection is highlighted on the desktop and the canvas.
        /// </summary>
        public bool IsHighlighterEnabled
        {
            get => _isHighlighterEnabled;
            set
            {
                // Show or hide the overlays and the SAP frame immediately.
                if (SetProperty(ref _isHighlighterEnabled, value))
                {
                    _ = RunAsync(ApplyHighlightAsync);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether live inspection is paused (Shift+F5).
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                // Pausing keeps the selection and generates its locators; resuming re-resolves the cursor position.
                if (!SetProperty(ref _isPaused, value))
                {
                    return;
                }

                _live.IsPaused = value;
                OnPropertyChanged(nameof(PauseLabel));

                if (value && TestHasSelection())
                {
                    _ = RunAsync(() => LoadLocatorsAsync(SelectedId));
                }
                else if (!value)
                {
                    _live.Reset();
                    HideMatches();
                }
            }
        }

        /// <summary>
        /// Gets the hint shown above the locator list.
        /// </summary>
        public string LocatorHint
        {
            get => _locatorHint;
            private set => SetProperty(ref _locatorHint, value);
        }

        /// <summary>
        /// Gets the generated locators for the selection.
        /// </summary>
        public ObservableCollection<LocatorCandidate> Locators { get; } = [];

        /// <summary>
        /// Gets the strategies offered by the locator tester.
        /// </summary>
        public IList<string> LocatorStrategies { get; } =
            [LocationStrategyModel.Xpath, LocationStrategyModel.SapId, LocationStrategyModel.TagName, LocationStrategyModel.CssSelector];

        /// <summary>
        /// Gets the command that navigates the tree; the parameter is an <see cref="InspectorNavigation"/> constant.
        /// </summary>
        public ICommand NavigateCommand { get; }

        /// <summary>
        /// Gets the pause button label.
        /// </summary>
        public string PauseLabel => IsPaused ? "▶ Resume (Shift+F5)" : "❚❚ Pause (Shift+F5)";

        /// <summary>
        /// Gets the properties of the selection; filtered through <see cref="PropertiesView"/>.
        /// </summary>
        public ObservableCollection<PropertyEntry> Properties { get; } = [];

        /// <summary>
        /// Gets the filtered property view bound to the grid.
        /// </summary>
        public ICollectionView PropertiesView { get; }

        /// <summary>
        /// Gets or sets the property filter text, matched against names and values.
        /// </summary>
        public string PropertyFilter
        {
            get => _propertyFilter;
            set
            {
                if (SetProperty(ref _propertyFilter, value))
                {
                    PropertiesView.Refresh();
                }
            }
        }

        /// <summary>
        /// Gets the quick actions for the selection.
        /// </summary>
        public ObservableCollection<QuickActionItem> QuickActions { get; } = [];

        /// <summary>
        /// Gets the command that reads the status bar into the status line.
        /// </summary>
        public ICommand ReadStatusBarCommand { get; }

        /// <summary>
        /// Gets the command that reloads the tree (and the canvas) from the selected source.
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// Gets the command that captures a new screenshot for the canvas.
        /// </summary>
        public ICommand RefreshScreenCommand { get; }

        /// <summary>
        /// Gets the root tree nodes: the SAP GUI application (local) or the attached session (driver).
        /// </summary>
        public ObservableCollection<TreeNodeViewModel> RootNodes { get; } = [];

        /// <summary>
        /// Gets the screen canvas of driver sessions.
        /// </summary>
        public ScreenViewModel Screen { get; } = new();

        /// <summary>
        /// Gets the identifier of the selection.
        /// </summary>
        public string SelectedId
        {
            get => _selectedId;
            private set => SetProperty(ref _selectedId, value);
        }

        /// <summary>
        /// Gets or sets the selected inspection source; selecting another source reloads everything from it.
        /// </summary>
        public IInspectorSource SelectedSource
        {
            get => _source;
            set
            {
                // Switch only to a different, known source.
                if (value is not null && !ReferenceEquals(value, _source))
                {
                    _ = RunAsync(() => SwitchSourceAsync(value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the virtual key chosen in the picker.
        /// </summary>
        public VirtualKeyEntry SelectedVirtualKey
        {
            get => _selectedVirtualKey;
            set => SetProperty(ref _selectedVirtualKey, value);
        }

        /// <summary>
        /// Gets the command that sends the chosen virtual key to the selection's window.
        /// </summary>
        public ICommand SendVirtualKeyCommand { get; }

        /// <summary>
        /// Gets the command that maximizes the selection's window.
        /// </summary>
        public ICommand SetWindowMaximizedCommand { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the grid shows all properties instead of the basic set.
        /// </summary>
        public bool ShowAllProperties
        {
            get => _showAllProperties;
            set
            {
                if (SetProperty(ref _showAllProperties, value))
                {
                    PropertiesView.Refresh();
                }
            }
        }

        /// <summary>
        /// Gets the inspection sources: local inspection first, then each connected driver session.
        /// </summary>
        public ObservableCollection<IInspectorSource> Sources { get; } = [];

        /// <summary>
        /// Gets the status line text.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Gets the command that evaluates the locator tester.
        /// </summary>
        public ICommand TestLocatorCommand { get; }

        /// <summary>
        /// Gets the locator tester's last result.
        /// </summary>
        public string TestResult
        {
            get => _testResult;
            private set => SetProperty(ref _testResult, value);
        }

        /// <summary>
        /// Gets or sets the locator tester's strategy.
        /// </summary>
        public string TestStrategy
        {
            get => _testStrategy;
            set => SetProperty(ref _testStrategy, value);
        }

        /// <summary>
        /// Gets or sets the locator tester's value.
        /// </summary>
        public string TestValue
        {
            get => _testValue;
            set => SetProperty(ref _testValue, value);
        }

        /// <summary>
        /// Gets the command that toggles live inspection.
        /// </summary>
        public ICommand TogglePauseCommand { get; }

        /// <summary>
        /// Gets the hint shown over an empty tree; empty while the tree has content.
        /// </summary>
        public string TreeHint
        {
            get => _treeHint;
            private set => SetProperty(ref _treeHint, value);
        }

        /// <summary>
        /// Gets the virtual keys of the selection's session.
        /// </summary>
        public ObservableCollection<VirtualKeyEntry> VirtualKeys { get; } = [];
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Adds a connected driver session to the selector and switches to it.
        /// </summary>
        /// <param name="source">The source returned by the Create session dialog.</param>
        /// <returns>A task that completes when the session's tree and screenshot are shown.</returns>
        public async Task ConnectAsync(DriverInspectorSource source)
        {
            // Require the source; the dialog returns one only on success.
            ArgumentNullException.ThrowIfNull(argument: source);

            // Add and switch; failures are reported in the status line like every other command.
            await RunAsync(async () =>
            {
                Sources.Add(source);
                await SwitchSourceAsync(source).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // Remove SAP GUI's frame before the scripting host shuts down.
            if (!string.IsNullOrEmpty(_visualized.Id))
            {
                _source.SetVisualizedAsync(_visualized.Id, _visualized.InnerObject, isOn: false).Wait(TimeSpan.FromSeconds(3));
                _visualized = default;
            }

            // Delete owned driver sessions, then close the overlays.
            foreach (var source in Sources.OfType<DriverInspectorSource>().ToList())
            {
                source.Dispose();
            }

            _highlighter.Dispose();
        }

        /// <summary>
        /// Loads the tree root from the selected source, and the screenshot when the source has a canvas.
        /// </summary>
        /// <returns>A task that completes when the root is shown.</returns>
        public async Task InitializeAsync()
        {
            // Read the root; local failures such as a closed SAP Logon are handled by the caller.
            // Announce the load, except during silent local retries, which keep the waiting message.
            var source = _source;
            var isLocal = source is LocalInspectorSource;
            if (!isLocal || _lastLocalError is null)
            {
                StatusText = isLocal ? "Attaching to SAP GUI…" : $"Loading {source.DisplayName}…";
            }

            var root = await source.GetRootAsync().ConfigureAwait(true);
            if (!ReferenceEquals(source, _source))
            {
                return;
            }

            // No root means nothing to show yet, such as no SAP Logon; wait without an error and retry on the interval.
            if (root is null)
            {
                ShowLocalWaiting();
                return;
            }

            // Replace the tree with the root node and expand its first level.
            RootNodes.Clear();
            var rootNode = NewTreeNode(root);
            RootNodes.Add(rootNode);
            await rootNode.LoadChildrenAsync().ConfigureAwait(true);
            rootNode.IsExpanded = true;
            TreeHint = string.Empty;

            if (source is LocalInspectorSource)
            {
                _isLocalAttached = true;
                _lastLocalError = null;
                StatusText = $"Attached: {rootNode.Children.Count} connection(s). Hover over a SAP GUI control.";
                return;
            }

            StatusText = $"{source.DisplayName}: {rootNode.Children.Count} window(s). Hover over the screenshot{(Features.HasDesktopOverlay ? " or SAP GUI" : string.Empty)}.";
            await RefreshScreenAsync().ConfigureAwait(true);
        }

        /// <summary>
        /// Loads the tree root; a local SAP GUI that is not running shows the waiting hint instead of an error.
        /// </summary>
        /// <returns>A task that completes when the root is shown or the state is reported.</returns>
        public async Task InitializeSafeAsync()
        {
            try
            {
                await InitializeAsync().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                ShowInitializeFailure(exception);
            }
        }

        /// <summary>
        /// Creates the view model of the Create session dialog, wired to the saved sets and the local sessions.
        /// </summary>
        /// <returns>The dialog view model; prefilling is offered only while local inspection is attached.</returns>
        public CreateSessionViewModel NewCreateSessionViewModel()
        {
            // Offer local prefill only when local inspection can read sessions.
            Func<Task<IList<SapConnectionInfoModel>>> loadLocalSessionsAsync = _isLocalAttached ? _localSource.GetConnectionsAsync : null;

            return new CreateSessionViewModel(newClient: url => new SapDriverClient(url), _setStore, loadLocalSessionsAsync);
        }

        /// <summary>
        /// Selects the control under a canvas position and pauses live inspection, like clicking in Appium Inspector.
        /// </summary>
        /// <param name="imageX">The horizontal position in image pixels.</param>
        /// <param name="imageY">The vertical position in image pixels.</param>
        /// <returns>A task that completes when the control is selected or the miss is reported.</returns>
        public Task OnCanvasClickAsync(double imageX, double imageY)
        {
            return RunAsync(async () =>
            {
                // Resolve the click directly; LiveInspector ignores samples while paused.
                var point = Screen.ConvertToScreenPoint(imageX, imageY);
                var hit = point is null ? null : await _source.HitTestAsync(point).ConfigureAwait(true);
                if (hit is null)
                {
                    StatusText = "No SAP GUI control at that position.";
                    return;
                }

                IsPaused = true;
                await SelectAsync(hit.Id, hit.InnerObject, SourceCanvas).ConfigureAwait(true);
            });
        }

        /// <summary>
        /// Feeds a canvas hover position to live inspection.
        /// </summary>
        /// <param name="imageX">The horizontal position in image pixels.</param>
        /// <param name="imageY">The vertical position in image pixels.</param>
        /// <returns>A task that completes when the sample is processed.</returns>
        public Task OnCanvasHoverAsync(double imageX, double imageY)
        {
            // Convert to screen coordinates; LiveInspector ignores unchanged positions and coalesces slow lookups.
            return _live.UpdateAsync(Screen.ConvertToScreenPoint(imageX, imageY));
        }

        /// <summary>
        /// Processes one cursor tick from the window timer.
        /// </summary>
        /// <param name="point">The desktop cursor sample, or null when the cursor is over the inspector.</param>
        /// <returns>A task that completes when the sample, any focus poll, and any local attach attempt are processed.</returns>
        public async Task OnTickAsync(InspectorPoint point)
        {
            // Retry local attachment periodically while SAP Logon is absent.
            if (_source is LocalInspectorSource && !_isLocalAttached)
            {
                await RetryLocalAsync().ConfigureAwait(true);
                return;
            }

            // Feed desktop hover only to sources whose coordinates match this desktop.
            if (Features.HasDesktopOverlay)
            {
                await _live.UpdateAsync(point).ConfigureAwait(true);
            }

            // Poll the keyboard focus at a lower rate when following focus.
            _tickCount++;
            var isFocusTick = _tickCount % FocusPollInterval == 0;
            if (isFocusTick && Features.CanFollowFocus && IsFollowFocus && !IsPaused && !_isFocusPolling && !string.IsNullOrEmpty(_snapshot?.SessionId))
            {
                await PollFocusAsync(_snapshot.SessionId).ConfigureAwait(true);
            }
        }

        // Tests whether a failure means "no SAP Logon on this machine", the normal state before SAP GUI starts.
        private static bool TestSapGuiNotRunning(Exception exception)
        {
            return exception is SapDriverException { ErrorKind: SapErrorKinds.SapGuiNotRunning };
        }

        // Shows or hides the selection highlights and SAP GUI's frame for the current snapshot.
        private async Task ApplyHighlightAsync()
        {
            // Hide everything when highlighting is off or nothing is selected.
            if (!IsHighlighterEnabled || _snapshot is null)
            {
                _highlighter.HideAll();
                Screen.ShowSelection(rectangle: null, label: string.Empty);
                await SetVisualizedAsync(id: null, innerObject: null).ConfigureAwait(true);
                return;
            }

            // Frame the control on the desktop and the canvas, as the source supports.
            var summary = _snapshot.Summary;
            var label = $"{summary.Type}  {summary.Name}";
            if (Features.HasDesktopOverlay)
            {
                _highlighter.ShowSelection(_snapshot.Rectangle, label);
            }

            Screen.ShowSelection(_snapshot.Rectangle, label);

            // Frame inner objects such as grid cells with SAP GUI's own frame, which only local COM can request.
            var innerObject = _live.Current?.Id == summary.Id ? _live.Current.InnerObject : string.Empty;
            await SetVisualizedAsync(summary.Id, innerObject).ConfigureAwait(true);
        }

        // Applies a snapshot to the details pane and quick actions.
        private void ApplySnapshot(ElementSnapshot snapshot, string innerObject)
        {
            _snapshot = snapshot;
            SelectedId = snapshot.Summary.Id;

            // Show the type, name, and the inner object SAP reported for complex controls.
            var inner = string.IsNullOrEmpty(innerObject) ? string.Empty : $"  ›  {innerObject}";
            Header = $"{snapshot.Summary.Type}  {snapshot.Summary.Name}{inner}";

            // Replace the property rows; the view filter applies the basic/all toggle.
            Properties.Clear();
            foreach (var property in snapshot.Properties)
            {
                Properties.Add(property);
            }

            // Offer the quick actions the source can perform on this control family.
            QuickActions.Clear();
            foreach (var action in _source.GetActions(snapshot.Summary.Type, snapshot.SubType))
            {
                QuickActions.Add(new QuickActionItem(action.Label, new RelayCommand(_ => _ = RunAsync(() => InvokeActionAsync(action)))));
            }
        }

        // Clears the tree, selection, details, and highlights, for example before switching sources.
        private async Task ClearStateAsync()
        {
            // Remove SAP GUI's frame through the source that drew it, then every overlay and highlight.
            await SetVisualizedAsync(id: null, innerObject: null).ConfigureAwait(true);
            _highlighter.HideAll();
            Screen.Clear();
            _live.Reset();

            // Invalidate pending selections and forget per-source state.
            _selectionVersion++;
            _snapshot = null;
            _lastFocusId = null;
            _virtualKeySessionId = null;
            SelectedId = string.Empty;
            Header = "Hover over a SAP GUI control";
            RootNodes.Clear();
            Properties.Clear();
            Locators.Clear();
            QuickActions.Clear();
            VirtualKeys.Clear();
            TestResult = string.Empty;
            InvokeResult = string.Empty;
            LocatorHint = PauseHint;
        }

        // Disconnects the selected driver session, deleting it when the inspector created it, and returns to local mode.
        private async Task DisconnectAsync()
        {
            // Only driver sessions can be disconnected; local inspection is always available.
            if (_source is not DriverInspectorSource driverSource)
            {
                return;
            }

            // Switch first, so nothing uses the source while it is deleted.
            await SwitchSourceAsync(_localSource).ConfigureAwait(true);
            Sources.Remove(driverSource);
            await driverSource.DisconnectAsync().ConfigureAwait(true);
            StatusText = driverSource.IsOwner
                ? $"Deleted the session {driverSource.SessionId}."
                : $"Detached from the session {driverSource.SessionId}; it stays open on the driver.";
        }

        // Removes the locator-match highlights on the desktop and the canvas.
        private void HideMatches()
        {
            // Hide both surfaces; the selection highlight stays.
            _highlighter.HideMatches();
            Screen.HideMatches();
        }

        // Performs a quick action, then refreshes the selection and screenshot because the screen may have changed.
        private async Task InvokeActionAsync(QuickAction action)
        {
            // Capture the target and text first, because the selection can change while the action runs.
            var id = SelectedId;
            var text = ActionText;

            await _source.InvokeActionAsync(id, action.Id, text).ConfigureAwait(true);
            StatusText = $"{action.Label} completed on {id}.";
            await RefreshAfterActionAsync(id).ConfigureAwait(true);
        }

        // Runs the generic member invoker on the selection.
        private async Task InvokeMemberAsync()
        {
            // Parse arguments on the UI thread so format errors are reported before reaching SAP GUI.
            var invocation = new MemberInvocation
            {
                Arguments = ArgumentParser.ConvertToArguments(InvokeArguments),
                Kind = InvokeKind,
                Name = InvokeName
            };

            InvokeResult = await _source.InvokeMemberAsync(SelectedId, invocation).ConfigureAwait(true);
            StatusText = $"{invocation.Kind} {invocation.Name} completed.";
        }

        // Generates and verifies locators for a selection.
        private async Task LoadLocatorsAsync(string id)
        {
            // Generate and verify the candidates through the source; verification evaluates each one in the session.
            LocatorHint = "Generating locators…";
            var locators = await _source.GenerateLocatorsAsync(id).ConfigureAwait(true);

            // Discard results for a selection that changed meanwhile.
            if (id != SelectedId)
            {
                return;
            }

            // Replace the list and explain an empty result.
            Locators.Clear();
            foreach (var locator in locators)
            {
                Locators.Add(locator);
            }

            LocatorHint = locators.Count == 0 ? "No locators above session level." : "Verified against the live session.";
        }

        // Loads the virtual keys for a session, once per session.
        private async Task LoadVirtualKeysAsync(string sessionId)
        {
            // Skip selections above session level and sessions whose keys are already loaded.
            if (string.IsNullOrEmpty(sessionId) || sessionId == _virtualKeySessionId)
            {
                return;
            }

            // Read the keys through the source: SAP GUI's own descriptions locally, the standard list for drivers.
            _virtualKeySessionId = sessionId;
            var keys = await _source.GetVirtualKeysAsync(sessionId).ConfigureAwait(true);

            // Replace the picker items and preselect the first key.
            VirtualKeys.Clear();
            foreach (var key in keys)
            {
                VirtualKeys.Add(key);
            }

            SelectedVirtualKey = VirtualKeys.FirstOrDefault();
        }

        // Moves the selection to a relative in the tree, pausing live mode so hover does not override it.
        private async Task NavigateAsync(string direction)
        {
            // Resolve the relative through the source; the end of a sibling list or the root has none.
            var id = SelectedId;
            var target = await _source.GetRelativeIdAsync(id, direction).ConfigureAwait(true);
            if (target is null)
            {
                StatusText = $"No {direction} for {id}.";
                return;
            }

            // Pause first, so a hover sample arriving meanwhile cannot replace the navigated selection.
            IsPaused = true;
            await SelectAsync(target, innerObject: string.Empty, SourceNavigation).ConfigureAwait(true);
        }

        // Creates a tree node wired to the current source's child loader and the tree selection flow.
        private TreeNodeViewModel NewTreeNode(ElementSummary summary)
        {
            // Capture the source, so nodes of a previous source never load from the new one.
            var source = _source;

            return new TreeNodeViewModel(
                summary,
                loadChildrenAsync: source.GetChildrenAsync,
                onSelected: node => _ = RunAsync(() => SelectAsync(node.Id, innerObject: string.Empty, SourceTree)));
        }

        // Selects the focused control when it changed since the last poll.
        private async Task PollFocusAsync(string sessionId)
        {
            // Mark the poll as running so overlapping ticks skip it.
            _isFocusPolling = true;
            try
            {
                // Reselect only when the focus moved since the last poll.
                var focusId = await _source.GetFocusedIdAsync(sessionId).ConfigureAwait(true);
                if (!string.IsNullOrEmpty(focusId) && focusId != _lastFocusId)
                {
                    _lastFocusId = focusId;
                    await SelectAsync(focusId, innerObject: string.Empty, SourceFocus).ConfigureAwait(true);
                }
            }
            finally
            {
                // Allow the next poll even when this one failed.
                _isFocusPolling = false;
            }
        }

        // Reads the status bar of the selection's session into the status line.
        private async Task ReadStatusBarAsync()
        {
            // Read the status bar through the source and show it, or say that it is empty.
            var text = await _source.GetStatusBarAsync(_snapshot.SessionId).ConfigureAwait(true);
            StatusText = string.IsNullOrEmpty(text) ? "The status bar is empty." : $"Status bar: {text}";
        }

        // Reloads the tree from the selected source; local mode also re-attaches after SAP Logon restarted.
        private async Task RefreshAsync()
        {
            // Clear everything, then load the tree again; local failures fall back to the waiting state.
            await ClearStateAsync().ConfigureAwait(true);
            await InitializeSafeAsync().ConfigureAwait(true);
        }

        // Reselects a control after an action and captures a new screenshot, because the screen may have changed.
        private async Task RefreshAfterActionAsync(string id)
        {
            // Capture the screen first, so the reselected control is drawn on the new screenshot.
            if (Features.HasScreenCanvas)
            {
                await RefreshScreenAsync().ConfigureAwait(true);
            }

            await SelectAsync(id, innerObject: string.Empty, SourceRefresh).ConfigureAwait(true);
        }

        // Captures a new screenshot of the session's current window for the canvas.
        private async Task RefreshScreenAsync()
        {
            // Only sources with a canvas provide screenshots.
            if (!Features.HasScreenCanvas)
            {
                return;
            }

            // Capture, then apply the image only when the source did not change while the capture ran.
            var source = _source;
            var capture = await source.GetScreenCaptureAsync().ConfigureAwait(true);
            if (ReferenceEquals(source, _source))
            {
                Screen.ApplyCapture(capture);
            }
        }

        // Retries local attachment every LocalRetryTicks ticks; the neutral waiting hint stays while SAP Logon is absent.
        private async Task RetryLocalAsync()
        {
            // Wait for the retry interval, and never run two attempts at once.
            if (_isLocalRetrying || ++_localRetryCount < LocalRetryTicks)
            {
                return;
            }

            // Attempt the attach; failures keep the waiting state and are reported once per distinct message.
            _localRetryCount = 0;
            _isLocalRetrying = true;
            try
            {
                await InitializeSafeAsync().ConfigureAwait(true);
            }
            finally
            {
                // Allow the next attempt after the interval.
                _isLocalRetrying = false;
            }
        }

        // Expands the tree along the ancestor path and selects the node without re-triggering a selection.
        private async Task RevealInTreeAsync(ElementSnapshot snapshot)
        {
            var node = RootNodes.FirstOrDefault();
            if (node is null)
            {
                return;
            }

            // Walk the path below the root, loading each level on demand; stop where the tree no longer matches.
            foreach (var ancestorId in snapshot.AncestorIds.Where(i => i != node.Id))
            {
                await node.LoadChildrenAsync().ConfigureAwait(true);
                node.IsExpanded = true;
                node = node.Children.FirstOrDefault(i => i.Id == ancestorId);
                if (node is null)
                {
                    return;
                }
            }

            // Select the target itself (or the root, when the target is the root).
            if (node.Id != snapshot.Summary.Id)
            {
                await node.LoadChildrenAsync().ConfigureAwait(true);
                node.IsExpanded = true;
                node = node.Children.FirstOrDefault(i => i.Id == snapshot.Summary.Id);
            }

            node?.SelectSilently();
        }

        // Runs a UI operation and reports any failure in the status line instead of crashing the inspector.
        private async Task RunAsync(Func<Task> operation)
        {
            try
            {
                await operation().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                ShowError(exception);
            }
        }

        // Selects an object: loads its snapshot, highlights it, syncs the tree, and loads locators for deliberate selections.
        private async Task SelectAsync(string id, string innerObject, string selectionSource)
        {
            // Tag the selection so a slower, older snapshot cannot overwrite it.
            var version = ++_selectionVersion;
            var snapshot = await _source.GetSnapshotAsync(id).ConfigureAwait(true);
            if (version != _selectionVersion)
            {
                return;
            }

            if (snapshot is null)
            {
                StatusText = $"{id} no longer exists.";
                return;
            }

            // Show the details and highlight; load the session's virtual keys when the session changed.
            ApplySnapshot(snapshot, innerObject);
            await ApplyHighlightAsync().ConfigureAwait(true);
            _ = RunAsync(() => LoadVirtualKeysAsync(snapshot.SessionId));

            // Reveal every selection except tree selections in the tree.
            if (selectionSource != SourceTree)
            {
                await RevealInTreeAsync(snapshot).ConfigureAwait(true);
            }

            // Generate locators for deliberate selections; hovering only shows the hint, to keep hover responsive.
            var isDeliberate = IsPaused || selectionSource is SourceTree or SourceNavigation or SourceFocus or SourceRefresh or SourceCanvas;
            if (isDeliberate)
            {
                await LoadLocatorsAsync(id).ConfigureAwait(true);
            }
            else
            {
                Locators.Clear();
                LocatorHint = PauseHint;
            }

            StatusText = $"Selected {id}";
        }

        // Sends the chosen virtual key to the selection's window (or the session's main window above window level).
        private async Task SendVirtualKeyAsync()
        {
            // Address the selection's window, or the main window for selections above window level.
            var windowId = string.IsNullOrEmpty(_snapshot.WindowId) ? $"{_snapshot.SessionId}/wnd[0]" : _snapshot.WindowId;
            var key = SelectedVirtualKey;

            await _source.SendVirtualKeyAsync(windowId, key.Key).ConfigureAwait(true);
            StatusText = $"Sent {key.Display} to {windowId}.";

            // Show the screen the key produced on the canvas.
            if (Features.HasScreenCanvas)
            {
                await RefreshScreenAsync().ConfigureAwait(true);
            }
        }

        // Copies text to the clipboard, reporting the rare clipboard-busy failure.
        private void SetClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
                StatusText = "Copied to the clipboard.";
            }
            catch (Exception exception) when (exception is System.Runtime.InteropServices.COMException or System.Runtime.InteropServices.ExternalException)
            {
                StatusText = "The clipboard is busy; try again.";
            }
        }

        // Moves SAP GUI's own frame to a new target, removing the previous frame first; null removes the frame.
        private async Task SetVisualizedAsync(string id, string innerObject)
        {
            // Only inner objects get SAP's frame, and only sources that can draw it.
            (string Id, string InnerObject) target = string.IsNullOrEmpty(innerObject) || !Features.CanVisualize ? (null, null) : (id, innerObject);
            if (target == _visualized)
            {
                return;
            }

            var previous = _visualized;
            _visualized = target;

            if (!string.IsNullOrEmpty(previous.Id))
            {
                await _source.SetVisualizedAsync(previous.Id, previous.InnerObject, isOn: false).ConfigureAwait(true);
            }

            if (!string.IsNullOrEmpty(target.Id))
            {
                await _source.SetVisualizedAsync(target.Id, target.InnerObject, isOn: true).ConfigureAwait(true);
            }
        }

        // Maximizes the selection's window.
        private async Task SetWindowMaximizedAsync()
        {
            // Maximize the selection's window through the source.
            var windowId = _snapshot.WindowId;

            await _source.SetWindowMaximizedAsync(windowId).ConfigureAwait(true);
            StatusText = $"Maximized {windowId}.";

            // Show the new window size on the canvas.
            if (Features.HasScreenCanvas)
            {
                await RefreshScreenAsync().ConfigureAwait(true);
            }
        }

        // Reports a failure in the status line; SAP failures show their W3C code and SAP error kind.
        private void ShowError(Exception exception)
        {
            StatusText = exception is SapDriverException driverException
                ? $"{driverException.Error} ({driverException.ErrorKind ?? "no kind"}): {driverException.Message}"
                : exception.GetBaseException().Message;
        }

        // Reports a failed tree load: a local SAP GUI that is not running is the neutral waiting state.
        private void ShowInitializeFailure(Exception exception)
        {
            // Driver failures are always errors.
            if (_source is not LocalInspectorSource)
            {
                TreeHint = "The driver session failed to load. Press Refresh, or disconnect.";
                ShowError(exception);
                return;
            }

            // A SAP Logon that exited between the check and the attach is the same neutral waiting state.
            if (TestSapGuiNotRunning(exception))
            {
                ShowLocalWaiting();
                return;
            }

            // Other local failures keep the waiting hint and are reported once until the message changes.
            _isLocalAttached = false;
            TreeHint = WaitingText;
            var message = exception.Message;
            if (message == _lastLocalError)
            {
                return;
            }

            _lastLocalError = message;
            ShowError(exception);
        }

        // Shows the neutral waiting state of local mode, announcing it once until the state changes.
        private void ShowLocalWaiting()
        {
            // Detach and show the hint; the cursor ticks retry the attach on their interval.
            _isLocalAttached = false;
            TreeHint = WaitingText;
            if (_lastLocalError == WaitingStatusText)
            {
                return;
            }

            // Record the state as the last report, so retries keep the status line quiet.
            _lastLocalError = WaitingStatusText;
            StatusText = WaitingStatusText;
        }

        // Switches to another source: clears every per-source state, then loads the new source's tree.
        private async Task SwitchSourceAsync(IInspectorSource source)
        {
            // Clear through the old source, so it removes its own SAP frame.
            await ClearStateAsync().ConfigureAwait(true);
            _source = source;
            _isLocalAttached = false;
            _lastLocalError = null;
            _localRetryCount = 0;

            // Publish the new source and its features, then load it.
            OnPropertyChanged(nameof(SelectedSource));
            OnPropertyChanged(nameof(Features));
            CommandManager.InvalidateRequerySuggested();
            await InitializeSafeAsync().ConfigureAwait(true);
        }

        // Evaluates the locator tester and highlights every match; pauses live mode so the highlights stay.
        private async Task TestLocatorAsync()
        {
            // Evaluate the locator through the source, then pause so hovering does not clear the highlights.
            var locator = new LocationStrategyModel { Using = TestStrategy, Value = TestValue };
            var matches = await _source.FindByLocatorAsync(_snapshot.SessionId, locator).ConfigureAwait(true);
            IsPaused = true;

            // Draw the matches on the desktop and the canvas, as the source supports.
            TestResult = matches.Count == 1 ? "1 match" : $"{matches.Count} matches";
            var highlights = matches.Select((hit, index) => (hit.Rectangle, $"#{index + 1}  {hit.Summary.Type}  {hit.Summary.Name}")).ToList();
            if (Features.HasDesktopOverlay)
            {
                _highlighter.ShowMatches(highlights);
            }

            Screen.ShowMatches(highlights);
        }

        // Tests whether any selection exists.
        private bool TestHasSelection()
        {
            return !string.IsNullOrEmpty(SelectedId);
        }

        // Tests whether a session-level selection exists, enabling session commands.
        private bool TestHasSession()
        {
            return !string.IsNullOrEmpty(_snapshot?.SessionId);
        }

        // Tests whether the selection is inside a session, enabling window commands.
        private bool TestHasWindow()
        {
            return TestHasSession();
        }

        // Filters the property grid by the basic/all toggle and the filter text.
        private bool TestPropertyVisible(object item)
        {
            if (item is not PropertyEntry entry || (!ShowAllProperties && !entry.IsBasic))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(PropertyFilter)
                || entry.Name.Contains(PropertyFilter, StringComparison.OrdinalIgnoreCase)
                || (entry.Value ?? string.Empty).Contains(PropertyFilter, StringComparison.OrdinalIgnoreCase);
        }
        #endregion
    }
}
