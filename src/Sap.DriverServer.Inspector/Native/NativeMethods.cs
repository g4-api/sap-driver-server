using System;
using System.Runtime.InteropServices;

namespace Sap.DriverServer.Inspector.Native
{
    /// <summary>
    /// Declares the Win32 functions the inspector uses for cursor tracking, window hit testing, overlays, and hotkeys.
    /// </summary>
    /// <remarks>
    /// The process is per-monitor DPI aware (see <c>app.manifest</c>), so every coordinate exchanged with these
    /// functions is in physical pixels, the same unit SAP GUI Scripting reports for <c>ScreenLeft</c> and <c>ScreenTop</c>.
    /// Signatures mirror the Win32 functions exactly, so they keep their native parameter counts.
    /// </remarks>
    internal static partial class NativeMethods
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the <c>DwmSetWindowAttribute</c> attribute that sets the window border color (Windows 11).
        /// </summary>
        public const int DwmwaBorderColor = 34;

        /// <summary>
        /// Specifies the <c>DwmSetWindowAttribute</c> attribute that sets the title bar color (Windows 11).
        /// </summary>
        public const int DwmwaCaptionColor = 35;

        /// <summary>
        /// Specifies the <c>DwmSetWindowAttribute</c> attribute that sets the title text color (Windows 11).
        /// </summary>
        public const int DwmwaTextColor = 36;

        /// <summary>
        /// Specifies the <c>DwmSetWindowAttribute</c> attribute that switches the title bar and caption buttons to dark mode.
        /// </summary>
        public const int DwmwaUseImmersiveDarkMode = 20;

        /// <summary>
        /// Specifies the <c>GetAncestor</c> flag that returns the top-level window.
        /// </summary>
        public const uint GaRoot = 2;

        /// <summary>
        /// Specifies the extended-window-style index for <c>GetWindowLongPtr</c>.
        /// </summary>
        public const int GwlExStyle = -20;

        /// <summary>
        /// Specifies the <c>MonitorFromPoint</c> flag that falls back to the nearest monitor.
        /// </summary>
        public const uint MonitorDefaultToNearest = 2;

        /// <summary>
        /// Specifies the hotkey modifier for Control.
        /// </summary>
        public const uint ModControl = 0x0002;

        /// <summary>
        /// Specifies the hotkey modifier that suppresses auto-repeat.
        /// </summary>
        public const uint ModNoRepeat = 0x4000;

        /// <summary>
        /// Specifies the hotkey modifier for Shift.
        /// </summary>
        public const uint ModShift = 0x0004;

        /// <summary>
        /// Specifies the <c>SetWindowPos</c> flag that does not activate the window.
        /// </summary>
        public const uint SwpNoActivate = 0x0010;

        /// <summary>
        /// Specifies the <c>SetWindowPos</c> flag that shows the window.
        /// </summary>
        public const uint SwpShowWindow = 0x0040;

        /// <summary>
        /// Specifies the hotkey notification message.
        /// </summary>
        public const int WmHotkey = 0x0312;

        /// <summary>
        /// Specifies the extended style that makes a layered window ignore mouse input.
        /// </summary>
        public const long WsExLayered = 0x00080000;

        /// <summary>
        /// Specifies the extended style that keeps a window from being activated.
        /// </summary>
        public const long WsExNoActivate = 0x08000000;

        /// <summary>
        /// Specifies the extended style that hides a window from Alt+Tab.
        /// </summary>
        public const long WsExToolWindow = 0x00000080;

        /// <summary>
        /// Specifies the extended style that lets mouse input pass through to the window below.
        /// </summary>
        public const long WsExTransparent = 0x00000020;

        /// <summary>
        /// Specifies the <c>SetWindowPos</c> insert-after value for "topmost".
        /// </summary>
        public static readonly IntPtr HwndTopmost = new(-1);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Sets a Desktop Window Manager attribute of a window, such as its title bar color.
        /// </summary>
        /// <param name="hwnd">The window.</param>
        /// <param name="attribute">The attribute, such as <see cref="DwmwaCaptionColor"/>.</param>
        /// <param name="value">The attribute value; colors are <c>COLORREF</c> values (<c>0x00BBGGRR</c>).</param>
        /// <param name="size">The size of the value in bytes.</param>
        /// <returns>An HRESULT; 0 on success, and an error for attributes this Windows version does not support.</returns>
        [LibraryImport("dwmapi.dll")]
        public static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>
        /// Gets the ancestor of a window.
        /// </summary>
        /// <param name="hwnd">The window.</param>
        /// <param name="flags">The ancestor kind, such as <see cref="GaRoot"/>.</param>
        /// <returns>The ancestor window handle.</returns>
        [LibraryImport("user32.dll")]
        public static partial IntPtr GetAncestor(IntPtr hwnd, uint flags);

        /// <summary>
        /// Gets the cursor position in physical screen pixels.
        /// </summary>
        /// <param name="point">The cursor position.</param>
        /// <returns>True on success.</returns>
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetCursorPos(out NativePoint point);

        /// <summary>
        /// Gets the effective DPI of a monitor.
        /// </summary>
        /// <param name="monitor">The monitor handle.</param>
        /// <param name="dpiType">The DPI kind; 0 is the effective DPI.</param>
        /// <param name="dpiX">The horizontal DPI.</param>
        /// <param name="dpiY">The vertical DPI.</param>
        /// <returns>An HRESULT; 0 on success.</returns>
        [LibraryImport("shcore.dll")]
        public static partial int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

        /// <summary>
        /// Gets a window's extended style.
        /// </summary>
        /// <param name="hwnd">The window.</param>
        /// <param name="index">The value index, such as <see cref="GwlExStyle"/>.</param>
        /// <returns>The value.</returns>
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        public static partial IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        /// <summary>
        /// Gets the process that owns a window.
        /// </summary>
        /// <param name="hwnd">The window.</param>
        /// <param name="processId">The owning process identifier.</param>
        /// <returns>The owning thread identifier.</returns>
        [LibraryImport("user32.dll")]
        public static partial uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        /// <summary>
        /// Gets the monitor that contains a point.
        /// </summary>
        /// <param name="point">The point in physical pixels.</param>
        /// <param name="flags">The fallback, such as <see cref="MonitorDefaultToNearest"/>.</param>
        /// <returns>The monitor handle.</returns>
        [LibraryImport("user32.dll")]
        public static partial IntPtr MonitorFromPoint(NativePoint point, uint flags);

        /// <summary>
        /// Registers a system-wide hotkey delivered to a window as <see cref="WmHotkey"/>.
        /// </summary>
        /// <param name="hwnd">The receiving window.</param>
        /// <param name="id">The hotkey identifier.</param>
        /// <param name="modifiers">The modifier flags.</param>
        /// <param name="virtualKey">The virtual key code.</param>
        /// <returns>True on success; false when another application owns the combination.</returns>
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

        /// <summary>
        /// Sets a window's extended style.
        /// </summary>
        /// <param name="hwnd">The window.</param>
        /// <param name="index">The value index, such as <see cref="GwlExStyle"/>.</param>
        /// <param name="value">The new value.</param>
        /// <returns>The previous value.</returns>
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        public static partial IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        /// <summary>
        /// Moves and sizes a window in physical pixels.
        /// </summary>
        /// <param name="hwnd">The window.</param>
        /// <param name="insertAfter">The z-order position, such as <see cref="HwndTopmost"/>.</param>
        /// <param name="x">The left edge.</param>
        /// <param name="y">The top edge.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="flags">The positioning flags.</param>
        /// <returns>True on success.</returns>
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        /// <summary>
        /// Unregisters a system-wide hotkey.
        /// </summary>
        /// <param name="hwnd">The receiving window.</param>
        /// <param name="id">The hotkey identifier.</param>
        /// <returns>True on success.</returns>
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnregisterHotKey(IntPtr hwnd, int id);

        /// <summary>
        /// Gets the window at a screen point, skipping click-through layered windows such as the highlight overlay.
        /// </summary>
        /// <param name="point">The point in physical pixels.</param>
        /// <returns>The window handle.</returns>
        [LibraryImport("user32.dll")]
        public static partial IntPtr WindowFromPoint(NativePoint point);
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents a Win32 <c>POINT</c> in physical pixels.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct NativePoint
        {
            /// <summary>
            /// The horizontal coordinate.
            /// </summary>
            public int X;

            /// <summary>
            /// The vertical coordinate.
            /// </summary>
            public int Y;
        }
        #endregion
    }
}
