using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;

using Sap.DriverServer.Inspector.Native;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Registers system-wide hotkeys for a window, so Shift+F5 and the navigation keys work while SAP GUI has focus.
    /// </summary>
    /// <remarks>
    /// Hotkeys are delivered as <c>WM_HOTKEY</c> to the window's message hook. A combination already owned by another
    /// application (for example Accessibility Insights) cannot be registered; <see cref="Register"/> reports that, and
    /// the window's own key bindings still work while the inspector has focus. Disposal unregisters every hotkey.
    /// </remarks>
    internal sealed class HotkeyManager : IDisposable
    {
        #region *** Fields       ***
        // Maps registered hotkey identifiers to their callbacks.
        private readonly Dictionary<int, Action> _callbacks = [];

        // Receives WM_HOTKEY; null after disposal.
        private HwndSource _source;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="HotkeyManager"/> class for a window that already has a native handle.
        /// </summary>
        /// <param name="window">The window that receives the hotkeys.</param>
        public HotkeyManager(Window window)
        {
            // Hook the window's message loop to receive WM_HOTKEY.
            _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
            _source?.AddHook(OnMessage);
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // Release every registration and the message hook.
            if (_source is null)
            {
                return;
            }

            foreach (var id in _callbacks.Keys)
            {
                NativeMethods.UnregisterHotKey(_source.Handle, id);
            }

            _callbacks.Clear();
            _source.RemoveHook(OnMessage);
            _source = null;
        }

        /// <summary>
        /// Registers one system-wide hotkey.
        /// </summary>
        /// <param name="modifiers">The modifier flags, such as <see cref="NativeMethods.ModShift"/>.</param>
        /// <param name="virtualKey">The Win32 virtual key code, such as 0x74 for F5.</param>
        /// <param name="callback">The action to run on the UI thread when the hotkey is pressed.</param>
        /// <returns>True when registered; false when another application owns the combination.</returns>
        public bool Register(uint modifiers, uint virtualKey, Action callback)
        {
            // Use sequential identifiers; suppress auto-repeat so holding a key toggles once.
            if (_source is null)
            {
                return false;
            }

            var id = _callbacks.Count + 1;
            var isRegistered = NativeMethods.RegisterHotKey(_source.Handle, id, modifiers | NativeMethods.ModNoRepeat, virtualKey);
            if (isRegistered)
            {
                _callbacks[id] = callback;
            }

            return isRegistered;
        }

        // Dispatches WM_HOTKEY to the registered callback; other messages pass through.
        // The signature is fixed by the HwndSourceHook delegate.
        private IntPtr OnMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == NativeMethods.WmHotkey && _callbacks.TryGetValue(wParam.ToInt32(), out var callback))
            {
                callback();
                handled = true;
            }

            return IntPtr.Zero;
        }
        #endregion
    }
}
