using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

using Sap.DriverServer.Inspector.Native;
using Sap.DriverServer.Inspector.Services;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Click-through, topmost overlay that frames one control and shows its type and name, like the Accessibility Insights highlighter.
    /// </summary>
    /// <remarks>
    /// The window is transparent to mouse input (<c>WS_EX_TRANSPARENT</c> on a layered window), never activates, and
    /// stays out of Alt+Tab, so it does not disturb SAP GUI or the hover hit test, which skips such windows. It is
    /// positioned with <c>SetWindowPos</c> in physical pixels from <see cref="HighlightGeometry"/>.
    /// </remarks>
    internal sealed class HighlightWindow : Window
    {
        #region *** Fields       ***
        // Shows the type and name of the highlighted control above its frame.
        private readonly TextBlock _label;

        // Holds the tag band whose row height follows the computed tag height.
        private readonly RowDefinition _tagRow;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="HighlightWindow"/> class.
        /// </summary>
        /// <param name="accent">The frame and tag color.</param>
        public HighlightWindow(Color accent)
        {
            // Configure a borderless, transparent, non-activating topmost window.
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Focusable = false;
            IsHitTestVisible = false;
            ResizeMode = ResizeMode.NoResize;
            ShowActivated = false;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStyle = WindowStyle.None;

            // Lay out a tag band above a frame that surrounds the control.
            var brush = new SolidColorBrush(accent);
            brush.Freeze();
            _label = new TextBlock
            {
                FontSize = 11,
                Foreground = Brushes.White,
                Margin = new Thickness(4, 0, 4, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            _tagRow = new RowDefinition { Height = new GridLength(HighlightGeometry.TagHeightDip) };

            var layout = new Grid();
            layout.RowDefinitions.Add(_tagRow);
            layout.RowDefinitions.Add(new RowDefinition());

            var tag = new Border { Background = brush, Child = _label, HorizontalAlignment = HorizontalAlignment.Left };
            var frame = new Border { BorderBrush = brush, BorderThickness = new Thickness(HighlightGeometry.BorderDip) };
            Grid.SetRow(frame, 1);
            layout.Children.Add(tag);
            layout.Children.Add(frame);

            Content = layout;
        }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Moves the overlay over a control and shows it.
        /// </summary>
        /// <param name="bounds">The overlay bounds in physical pixels.</param>
        /// <param name="label">The tag text, such as <c>GuiButton btn[11]</c>.</param>
        public void ShowAt(HighlightGeometry.HighlightBounds bounds, string label)
        {
            // Update the tag, then show the window before positioning so it has a native handle.
            _label.Text = label;
            if (!IsVisible)
            {
                Show();
            }

            // Position in physical pixels; WPF derives the DIP layout from the window's monitor DPI.
            var handle = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HwndTopmost,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
        }

        /// <inheritdoc />
        protected override void OnSourceInitialized(EventArgs e)
        {
            // Make the native window click-through, non-activating, and hidden from Alt+Tab once it exists.
            base.OnSourceInitialized(e);

            var handle = new WindowInteropHelper(this).Handle;
            var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64();
            var overlayStyle = NativeMethods.WsExTransparent | NativeMethods.WsExLayered | NativeMethods.WsExToolWindow | NativeMethods.WsExNoActivate;
            NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, new IntPtr(style | overlayStyle));
        }
        #endregion
    }
}
