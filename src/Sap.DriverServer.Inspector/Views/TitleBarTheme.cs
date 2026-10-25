using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

using Sap.DriverServer.Inspector.Native;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Paints a window's native title bar in the G4 theme colors, so it matches the dark client area.
    /// </summary>
    /// <remarks>
    /// The colors come from the theme tokens in <c>Views/Themes/G4Theme.xaml</c>, which stay the single source of truth.
    /// Windows 11 applies the caption, text, and border colors; Windows 10 supports only the dark mode switch and keeps
    /// its own dark gray. Older versions ignore every attribute and keep the default title bar, which remains usable.
    /// </remarks>
    internal static class TitleBarTheme
    {
        #region *** Constants    ***
        // Names the theme token for the window border, the default border color of every surface.
        private const string BorderColorKey = "G4BorderPrimaryColor";

        // Names the theme token for the title bar, the window background, so the caption and client area read as one.
        private const string CaptionColorKey = "G4BackgroundPrimaryColor";

        // Names the theme token for the title text.
        private const string TextColorKey = "G4TextPrimaryColor";
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Applies the theme colors to a window's title bar as soon as the window has a native handle.
        /// </summary>
        /// <param name="window">The window, before it is shown.</param>
        public static void Apply(Window window)
        {
            // Require the window; the attributes need its native handle, which exists from SourceInitialized on.
            ArgumentNullException.ThrowIfNull(argument: window);

            window.SourceInitialized += (_, _) => SetAttributes(window);
        }

        // Converts a WPF color to a Win32 COLORREF, which stores red in the low byte (0x00BBGGRR).
        private static int ConvertToColorReference(Color color)
        {
            // Pack the channels in Win32 order; the alpha channel has no meaning for window frame colors.
            return color.R | (color.G << 8) | (color.B << 16);
        }

        // Sets one attribute, ignoring the result: an unsupported attribute leaves the default, usable title bar.
        private static void SetAttribute(IntPtr handle, int attribute, int value)
        {
            // Pass the value by reference with its size, as DwmSetWindowAttribute reads it from memory.
            _ = NativeMethods.DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int));
        }

        // Switches the title bar to dark mode, then applies the exact theme colors where Windows supports them.
        private static void SetAttributes(Window window)
        {
            // Dark mode first: it also darkens the caption buttons, and it is the only attribute Windows 10 knows.
            var handle = new WindowInteropHelper(window).Handle;
            SetAttribute(handle, NativeMethods.DwmwaUseImmersiveDarkMode, value: 1);

            // Apply each theme color; a token missing from the resources keeps the Windows default for that part.
            SetColor(window, handle, NativeMethods.DwmwaCaptionColor, CaptionColorKey);
            SetColor(window, handle, NativeMethods.DwmwaTextColor, TextColorKey);
            SetColor(window, handle, NativeMethods.DwmwaBorderColor, BorderColorKey);
        }

        // Applies the color of one theme token to one title bar attribute.
        private static void SetColor(Window window, IntPtr handle, int attribute, string resourceKey)
        {
            // Resolve the token through the window, so a theme merged at application level is found.
            if (window.TryFindResource(resourceKey) is Color color)
            {
                SetAttribute(handle, attribute, ConvertToColorReference(color));
            }
        }
        #endregion
    }
}
