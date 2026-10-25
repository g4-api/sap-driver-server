using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;
using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiVComponent</c>: a visible control with text, a tooltip, geometry, focus, and highlighting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each object wraps one WPF element of a frame window. <c>ScreenLeft</c>, <c>ScreenTop</c>, <c>Width</c>, and
    /// <c>Height</c> are physical screen pixels, rounded the same way for every control; they are zero when the control
    /// is not displayed. Writing <c>Text</c> raises an error unless the control type supports text input.
    /// </para>
    /// <para>
    /// <c>Visualize</c> draws a red frame, as SAP GUI does. Controls with inner objects, such as grid cells, frame the
    /// inner object named by the second argument.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public abstract class GuiVComponent : GuiComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiVComponent"/> class.
        /// </summary>
        /// <param name="element">The WPF element; it belongs to a simulator frame window.</param>
        protected GuiVComponent(FrameworkElement element)
            : base(GetFrame(element).Session.System)
        {
            Element = element;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the control accepts input.
        /// </summary>
        public virtual bool Changeable => Element.IsEnabled;

        /// <summary>
        /// Gets the height in physical pixels; zero when the control is not displayed.
        /// </summary>
        public int Height => GetPixelBounds().Height;

        /// <inheritdoc />
        public override string Id => Frame.Id + "/" + ScriptingTree.GetRelativeId(Element);

        /// <inheritdoc />
        public override object Parent
        {
            get
            {
                // Return the nearest scriptable ancestor, or the window for top-level areas such as usr.
                var parent = ScriptingTree.GetParentElement(Element);

                return parent is null ? GuiComponentFactory.NewWindow(Frame) : GuiComponentFactory.NewComponent(parent);
            }
        }

        /// <summary>
        /// Gets the horizontal screen coordinate of the left edge in physical pixels.
        /// </summary>
        public int ScreenLeft => GetPixelBounds().Left;

        /// <summary>
        /// Gets the vertical screen coordinate of the top edge in physical pixels.
        /// </summary>
        public int ScreenTop => GetPixelBounds().Top;

        /// <summary>
        /// Gets or sets the text; setting it raises an error for controls without text input.
        /// </summary>
        public string Text
        {
            get => GetText();
            set => SetText(value);
        }

        /// <summary>
        /// Gets the tooltip; empty when the control has none.
        /// </summary>
        public string Tooltip => Element.ToolTip as string ?? string.Empty;

        /// <summary>
        /// Gets the width in physical pixels; zero when the control is not displayed.
        /// </summary>
        public int Width => GetPixelBounds().Width;

        /// <summary>
        /// Gets the wrapped WPF element.
        /// </summary>
        internal FrameworkElement Element { get; }

        /// <summary>
        /// Gets the frame window that shows the element.
        /// </summary>
        internal FrameWindow Frame => GetFrame(Element);

        /// <inheritdoc />
        internal override string Segment => ScriptingTree.GetId(Element);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Moves the keyboard focus to the control.
        /// </summary>
        public void SetFocus()
        {
            // Move the keyboard focus inside the element's window without activating the window.
            Element.Focus();
        }

        /// <summary>
        /// Draws or removes a red frame around the control or one of its inner objects.
        /// </summary>
        /// <param name="on">True to draw the frame; false to remove it.</param>
        /// <param name="innerObject">The inner object, such as <c>Cell(2,PRICE)</c>; empty for the control itself.</param>
        /// <returns>True when the frame state changed; false when the inner object is unknown.</returns>
        public bool Visualize(bool on, [Optional, DefaultParameterValue("")] string innerObject)
        {
            // Resolve the frame's target; controls without inner objects accept only an empty description.
            var target = GetHighlightTarget(innerObject ?? string.Empty);

            return target is not null && Frame.SetHighlighted(target, on);
        }

        /// <summary>
        /// Describes the inner object under a screen point, such as a grid cell.
        /// </summary>
        /// <param name="point">The screen point in physical pixels.</param>
        /// <returns>The description, such as <c>Cell(2,PRICE)</c>; empty for controls without inner objects.</returns>
        internal virtual string GetInnerObject(Point point)
        {
            return string.Empty;
        }

        /// <summary>
        /// Tests whether the control is displayed under a screen point.
        /// </summary>
        /// <param name="point">The screen point in physical pixels.</param>
        /// <returns>True when the point is inside the control's displayed rectangle.</returns>
        internal bool TestContainsPoint(Point point)
        {
            // Compare against the unrounded rectangle; hidden controls contain no point.
            var bounds = ScriptingTree.GetScreenBounds(Element);

            return !bounds.IsEmpty && bounds.Contains(point);
        }

        /// <summary>
        /// Gets the element to frame for an inner-object description.
        /// </summary>
        /// <param name="innerObject">The description; empty for the control itself.</param>
        /// <returns>The element to frame; null when the description names nothing.</returns>
        protected virtual FrameworkElement GetHighlightTarget(string innerObject)
        {
            return innerObject.Length == 0 ? Element : null;
        }

        /// <summary>
        /// Gets the control's text, read from its WPF element.
        /// </summary>
        /// <returns>The text; empty for controls without text.</returns>
        protected virtual string GetText()
        {
            return ConvertToText(Element);
        }

        /// <summary>
        /// Sets the control's text.
        /// </summary>
        /// <param name="value">The new text.</param>
        /// <exception cref="SimulatorException">The control has no text input.</exception>
        protected virtual void SetText(string value)
        {
            throw new SimulatorException($"The text of '{Id}' is not changeable.");
        }

        // Removes the access-key marker from a header, as in "_Simulator" to "Simulator".
        private static string ConvertToHeaderText(object header)
        {
            return (header as string ?? string.Empty).Replace("_", string.Empty, StringComparison.Ordinal);
        }

        // Reads the visible text of an element: text boxes and blocks, headers, string content, or a decorated child.
        private static string ConvertToText(object element)
        {
            // Read the text the element shows, by element kind; other elements have none.
            return element switch
            {
                TextBox textBox => textBox.Text,
                TextBlock textBlock => textBlock.Text,
                HeaderedItemsControl headered => ConvertToHeaderText(headered.Header),
                HeaderedContentControl headered => ConvertToHeaderText(headered.Header),
                ContentControl content => content.Content as string ?? string.Empty,
                Decorator decorator => ConvertToText(decorator.Child),
                _ => string.Empty
            };
        }

        // Gets the frame window of an element; every scriptable element lives in one.
        private static FrameWindow GetFrame(FrameworkElement element)
        {
            // Require the element and find the simulator window that shows it.
            ArgumentNullException.ThrowIfNull(argument: element);

            return Window.GetWindow(element) as FrameWindow
                ?? throw new SimulatorException("The element is not shown in a simulator window.");
        }

        // Rounds the displayed rectangle to whole pixels; every control uses the same rounding, so edges line up.
        private (int Left, int Top, int Width, int Height) GetPixelBounds()
        {
            // Report zero geometry for controls that are not displayed.
            var bounds = ScriptingTree.GetScreenBounds(Element);
            if (bounds.IsEmpty)
            {
                return (0, 0, 0, 0);
            }

            var left = (int)Math.Round(bounds.Left);
            var top = (int)Math.Round(bounds.Top);

            return (left, top, (int)Math.Round(bounds.Right) - left, (int)Math.Round(bounds.Bottom) - top);
        }
        #endregion
    }
}
