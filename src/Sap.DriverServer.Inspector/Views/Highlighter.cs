using System;
using System.Collections.Generic;
using System.Windows.Media;

using Sap.DriverServer.Inspector.Native;
using Sap.DriverServer.Inspector.Services;
using Sap.DriverServer.Models;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Manages a pool of <see cref="HighlightWindow"/> overlays: one for the selection, several for locator matches.
    /// </summary>
    /// <remarks>
    /// Windows are created on first use and hidden rather than closed, so moving the highlight between controls does not
    /// create native windows on every hover. Every member runs on the UI thread. Disposal closes all overlays.
    /// </remarks>
    public sealed class Highlighter : IDisposable
    {
        #region *** Constants    ***
        // Colors the selection overlay, matching the Windows accent blue.
        private static readonly Color SelectionColor = Color.FromRgb(0x00, 0x78, 0xD4);

        // Colors locator-match overlays, distinct from the selection.
        private static readonly Color MatchColor = Color.FromRgb(0xCA, 0x50, 0x10);
        #endregion

        #region *** Fields       ***
        // Holds the overlays for locator matches; reused across tests of different locators.
        private readonly List<HighlightWindow> _matchWindows = [];

        // Holds the selection overlay, created on first use.
        private HighlightWindow _selectionWindow;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Dispose()
        {
            // Close every overlay; the pool is not reused afterwards.
            _selectionWindow?.Close();
            _selectionWindow = null;

            foreach (var window in _matchWindows)
            {
                window.Close();
            }

            _matchWindows.Clear();
        }

        /// <summary>
        /// Hides every overlay without closing it.
        /// </summary>
        public void HideAll()
        {
            // Hide the selection and the match overlays.
            _selectionWindow?.Hide();
            HideMatches();
        }

        /// <summary>
        /// Hides the locator-match overlays.
        /// </summary>
        public void HideMatches()
        {
            foreach (var window in _matchWindows)
            {
                window.Hide();
            }
        }

        /// <summary>
        /// Highlights every locator match with a numbered tag, reusing pooled overlays.
        /// </summary>
        /// <param name="matches">The match rectangles and tag labels.</param>
        public void ShowMatches(IList<(RectangleModel Rectangle, string Label)> matches)
        {
            // Grow the pool as needed, then position one overlay per match and hide the rest.
            HideMatches();
            for (var index = 0; index < matches.Count; index++)
            {
                if (index == _matchWindows.Count)
                {
                    _matchWindows.Add(new HighlightWindow(MatchColor));
                }

                ShowWindow(_matchWindows[index], matches[index].Rectangle, matches[index].Label);
            }
        }

        /// <summary>
        /// Highlights the selected control, or hides the selection overlay when the control has no geometry.
        /// </summary>
        /// <param name="rectangle">The control rectangle in physical pixels.</param>
        /// <param name="label">The tag text.</param>
        public void ShowSelection(RectangleModel rectangle, string label)
        {
            // Create the selection overlay on first use.
            _selectionWindow ??= new HighlightWindow(SelectionColor);
            ShowWindow(_selectionWindow, rectangle, label);
        }

        // Returns the scale factor of the monitor that contains a point, falling back to 1 when DPI is unavailable.
        private static double GetScale(int x, int y)
        {
            var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.NativePoint { X = x, Y = y }, NativeMethods.MonitorDefaultToNearest);
            var result = NativeMethods.GetDpiForMonitor(monitor, dpiType: 0, out var dpiX, out _);

            return result == 0 && dpiX > 0 ? dpiX / 96.0 : 1.0;
        }

        // Positions one overlay over a rectangle, hiding it when the rectangle is empty.
        private static void ShowWindow(HighlightWindow window, RectangleModel rectangle, string label)
        {
            // Scale with the DPI of the monitor that holds the control's top-left corner.
            var bounds = HighlightGeometry.ConvertToWindowBounds(rectangle, GetScale(rectangle.Left, rectangle.Top));
            if (bounds is null)
            {
                window.Hide();
                return;
            }

            window.ShowAt(bounds, label);
        }
        #endregion
    }
}
