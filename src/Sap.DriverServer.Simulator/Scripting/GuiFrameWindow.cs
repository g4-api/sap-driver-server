using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;
using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiFrameWindow</c>: a session window with its handle, focus, screenshots, and virtual keys.
    /// </summary>
    /// <remarks>
    /// The window's geometry is its client content, which is also what <c>HardCopy</c> captures, so screenshot pixels map
    /// onto <c>ScreenLeft</c> and <c>ScreenTop</c> one to one. <c>Handle</c> is the Win32 handle of the top-level window.
    /// </remarks>
    [ComVisible(true)]
    public abstract class GuiFrameWindow : GuiVContainer
    {
        #region *** Fields       ***
        // Holds the WPF window this object represents.
        private readonly FrameWindow _frame;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiFrameWindow"/> class.
        /// </summary>
        /// <param name="frame">The simulator window.</param>
        protected GuiFrameWindow(FrameWindow frame)
            : base(GetContentRoot(frame))
        {
            // Keep the window; identity, handle, and keys come from it rather than from its content.
            _frame = frame;
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public override bool Changeable => true;

        /// <summary>
        /// Gets the control that has the keyboard focus in this window; null when no control has it.
        /// </summary>
        public object GuiFocus
        {
            get
            {
                // Map the focused WPF part, such as a text box's inner host, to the control that owns it.
                var element = ScriptingTree.FindElement(FocusManager.GetFocusedElement(_frame) as DependencyObject);

                return element is null ? null : GuiComponentFactory.NewComponent(element);
            }
        }

        /// <summary>
        /// Gets the Win32 window handle.
        /// </summary>
        public int Handle => (int)_frame.Handle;

        /// <summary>
        /// Gets a value indicating whether the window is minimized.
        /// </summary>
        public bool Iconic => _frame.WindowState == WindowState.Minimized;

        /// <inheritdoc />
        public override string Id => _frame.Id;

        /// <inheritdoc />
        public override object Parent => new GuiSession(_frame.Session);

        /// <inheritdoc />
        internal override string Segment => string.Create(CultureInfo.InvariantCulture, $"wnd[{_frame.WindowIndex}]");
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Closes the window: the popup is cancelled, and closing the main window ends the session.
        /// </summary>
        public void Close()
        {
            // Close the window; the session handles the outcome, a cancelled popup or an ended session.
            _frame.Close();
        }

        /// <summary>
        /// Writes an image of the window's client content to a file.
        /// </summary>
        /// <param name="fileName">The target file; a relative name is placed in the temporary folder.</param>
        /// <param name="imageType">The format: 0 BMP (default), 1 JPEG, 2 PNG, or 3 GIF.</param>
        /// <returns>The full path of the written file.</returns>
        public string HardCopy(string fileName, [Optional, DefaultParameterValue(0)] int imageType)
        {
            return _frame.SaveImage(fileName, imageType);
        }

        /// <summary>
        /// Minimizes the window.
        /// </summary>
        public void Iconify()
        {
            // Minimize the window; scripting keeps working on minimized windows.
            _frame.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Tests whether the window accepts a virtual key.
        /// </summary>
        /// <param name="key">The virtual key.</param>
        /// <returns>True when the key has a function in this window and the window is not blocked.</returns>
        public bool IsVKeyAllowed(int key)
        {
            return _frame.Session.TestVirtualKey(_frame.WindowIndex, key);
        }

        /// <summary>
        /// Maximizes the window.
        /// </summary>
        public void Maximize()
        {
            // Maximize the window; its geometry changes with the next layout pass.
            _frame.WindowState = WindowState.Maximized;
        }

        /// <summary>
        /// Restores the window from the minimized or maximized state.
        /// </summary>
        public void Restore()
        {
            // Return the window to its normal size.
            _frame.WindowState = WindowState.Normal;
        }

        /// <summary>
        /// Sends a virtual key to the window, such as 0 for Enter or 3 for Back.
        /// </summary>
        /// <param name="key">The virtual key.</param>
        public void SendVKey(int key)
        {
            // Let the session run the key, which refuses keys for a blocked window.
            _frame.Session.SendVirtualKey(_frame.WindowIndex, key);
        }

        /// <inheritdoc />
        protected override string GetText()
        {
            // Report the SAP window title, without the simulator suffix of the Windows caption.
            return _frame.ScreenTitle;
        }

        // Requires the window before the base constructor runs, and returns the content scripting measures.
        private static FrameworkElement GetContentRoot(FrameWindow frame)
        {
            // Report a missing window under its own name, not as a missing element.
            ArgumentNullException.ThrowIfNull(argument: frame);

            return frame.ContentRoot;
        }
        #endregion
    }
}
