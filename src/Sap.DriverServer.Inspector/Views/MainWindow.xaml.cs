using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using Sap.DriverServer.Inspector.Models;
using Sap.DriverServer.Inspector.Native;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Inspector.ViewModels;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Hosts the inspector UI and owns the platform pieces the view model cannot: cursor sampling and global hotkeys.
    /// </summary>
    /// <remarks>
    /// A <see cref="DispatcherTimer"/> samples the cursor every <see cref="SampleIntervalMilliseconds"/>, resolves the
    /// top-level window under it, and passes the sample to the view model; samples over the inspector's own windows are
    /// passed as null so they are ignored. The same timer drives the local SAP GUI retry. Canvas mouse events and the
    /// Create session dialog are forwarded to the view model. Shift+F5 and Ctrl+Shift+F5 to F9 are registered system-wide, so they work
    /// while SAP GUI has focus; the XAML key bindings cover combinations another application already owns.
    /// </remarks>
    public partial class MainWindow : Window
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the cursor sampling interval, close to the Accessibility Insights hover cadence.
        /// </summary>
        public const int SampleIntervalMilliseconds = 150;

        // Holds the Win32 virtual key code for F5 (pause, and previous sibling with Ctrl+Shift).
        private const uint VirtualKeyF5 = 0x74;

        // Holds the Win32 virtual key code for F6 (parent with Ctrl+Shift).
        private const uint VirtualKeyF6 = 0x75;

        // Holds the Win32 virtual key code for F7 (first child with Ctrl+Shift).
        private const uint VirtualKeyF7 = 0x76;

        // Holds the Win32 virtual key code for F8 (next sibling with Ctrl+Shift).
        private const uint VirtualKeyF8 = 0x77;

        // Holds the Win32 virtual key code for F9 (last child with Ctrl+Shift).
        private const uint VirtualKeyF9 = 0x78;
        #endregion

        #region *** Fields       ***
        // Identifies this process, so samples over the inspector's own windows are ignored.
        private readonly uint _processId = (uint)Environment.ProcessId;

        // Samples the cursor on the UI thread.
        private readonly DispatcherTimer _timer;

        // Owns the system-wide hotkey registrations; created once the window has a native handle.
        private HotkeyManager _hotkeys;

        // Prevents overlapping ticks while the previous sample is still being processed.
        private bool _isTicking;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        /// <param name="viewModel">The view model bound to the window.</param>
        public MainWindow(MainViewModel viewModel)
        {
            // Build the view with a G4 title bar, bind the view model, and prepare the sampling timer; it starts once loaded.
            InitializeComponent();
            TitleBarTheme.Apply(this);
            DataContext = viewModel;
            ViewModel = viewModel;

            _timer = new DispatcherTimer(DispatcherPriority.Input)
            {
                Interval = TimeSpan.FromMilliseconds(SampleIntervalMilliseconds)
            };
            _timer.Tick += OnTimerTick;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the bound view model.
        /// </summary>
        public MainViewModel ViewModel { get; }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override void OnClosed(EventArgs e)
        {
            // Stop sampling and release the hotkeys before the application disposes the view model.
            _timer.Stop();
            _hotkeys?.Dispose();
            base.OnClosed(e);
        }

        /// <inheritdoc />
        protected override async void OnContentRendered(EventArgs e)
        {
            // Register hotkeys once the native handle exists, load the tree, then start sampling.
            base.OnContentRendered(e);
            RegisterHotkeys();

            await ViewModel.InitializeSafeAsync().ConfigureAwait(true);
            _timer.Start();
        }

        // Builds a cursor sample, or null when the cursor is over one of the inspector's own windows.
        private InspectorPoint GetCursorSample()
        {
            if (!NativeMethods.GetCursorPos(out var point))
            {
                return null;
            }

            // Hit-test the top-level window; click-through overlays are skipped by WindowFromPoint.
            var window = NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(point), NativeMethods.GaRoot);
            NativeMethods.GetWindowThreadProcessId(window, out var ownerId);

            return ownerId == _processId ? null : new InspectorPoint(point.X, point.Y, window.ToInt64());
        }

        // Feeds one cursor sample to the view model, skipping the tick when the previous one is still running.
        private async void OnTimerTick(object sender, EventArgs e)
        {
            if (_isTicking)
            {
                return;
            }

            _isTicking = true;
            try
            {
                await ViewModel.OnTickAsync(GetCursorSample()).ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                // Keep sampling after an unexpected failure; the view model reports SAP failures itself.
                Debug.WriteLine(exception);
            }
            finally
            {
                _isTicking = false;
            }
        }

        // Opens the Create session dialog and switches to the new driver session when the dialog succeeds.
        private async void OnCreateSessionClick(object sender, RoutedEventArgs e)
        {
            // Show the dialog modally over the inspector; Cancel leaves the current source unchanged.
            var dialogModel = ViewModel.NewCreateSessionViewModel();
            var dialog = new CreateSessionWindow(dialogModel) { Owner = this };
            if (dialog.ShowDialog() != true || dialogModel.Result is null)
            {
                return;
            }

            // Hand the source to the view model, which reports its own failures in the status line.
            await ViewModel.ConnectAsync(dialogModel.Result).ConfigureAwait(true);
        }

        // Selects the control under a canvas click; positions are in image pixels because the surface is image-sized.
        private void OnScreenMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Read the position relative to the image-sized surface; the view model reports its own failures.
            var position = e.GetPosition(ScreenSurface);
            _ = ViewModel.OnCanvasClickAsync(position.X, position.Y);
        }

        // Feeds canvas hover positions, in image pixels, to live inspection.
        private void OnScreenMouseMove(object sender, MouseEventArgs e)
        {
            // Read the position relative to the image-sized surface; live inspection coalesces fast moves.
            var position = e.GetPosition(ScreenSurface);
            _ = ViewModel.OnCanvasHoverAsync(position.X, position.Y);
        }

        // Brings a tree item into view when it is selected, including programmatic selections after hover.
        private void OnTreeItemSelected(object sender, RoutedEventArgs e)
        {
            if (sender is TreeViewItem item && ReferenceEquals(e.OriginalSource, item))
            {
                item.BringIntoView();
            }
        }

        // Registers the Accessibility Insights hotkeys system-wide and reports combinations owned by other applications.
        private void RegisterHotkeys()
        {
            _hotkeys = new HotkeyManager(window: this);
            var controlShift = NativeMethods.ModControl | NativeMethods.ModShift;
            var isRegistered = _hotkeys.Register(NativeMethods.ModShift, VirtualKeyF5, () => ViewModel.TogglePauseCommand.Execute(parameter: null))
                & _hotkeys.Register(controlShift, VirtualKeyF5, () => Navigate(InspectorNavigation.PreviousSibling))
                & _hotkeys.Register(controlShift, VirtualKeyF6, () => Navigate(InspectorNavigation.Parent))
                & _hotkeys.Register(controlShift, VirtualKeyF7, () => Navigate(InspectorNavigation.FirstChild))
                & _hotkeys.Register(controlShift, VirtualKeyF8, () => Navigate(InspectorNavigation.NextSibling))
                & _hotkeys.Register(controlShift, VirtualKeyF9, () => Navigate(InspectorNavigation.LastChild));

            if (!isRegistered)
            {
                Title += "  (some hotkeys are owned by another application and work only while this window has focus)";
            }

            return;

            // Runs a navigation command when it is currently available.
            void Navigate(string direction)
            {
                if (ViewModel.NavigateCommand.CanExecute(direction))
                {
                    ViewModel.NavigateCommand.Execute(direction);
                }
            }
        }
        #endregion
    }
}
