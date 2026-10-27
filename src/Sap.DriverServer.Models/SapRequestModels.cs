using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the request body of the SAP-specific "send virtual key" endpoint.
    /// </summary>
    /// <remarks>
    /// SAP virtual keys are the numeric function-key codes accepted by <c>GuiFrameWindow.sendVKey</c>,
    /// for example 0 (Enter), 3 (F3, Back), or 82 (Page Down).
    /// </remarks>
    public class SapVirtualKeyModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the SAP virtual key code.
        /// </summary>
        [Range(0, 99)]
        public int VirtualKey { get; set; }

        /// <summary>
        /// Gets or sets the target window name, such as <c>wnd[1]</c>; null or empty targets the session's active window.
        /// </summary>
        public string Window { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents the request body of the SAP-specific "call element method" endpoint.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The method must be on the driver's allow-list for the element's control family, such as
    /// <c>pressToolbarButton</c> for a grid view shell or <c>expandNode</c> for a tree shell. Method names are matched
    /// case-insensitively.
    /// </para>
    /// <para>
    /// Arguments are positional JSON scalars: strings, numbers, Booleans, or null. Whole numbers are passed as integers,
    /// so row indexes and positions reach SAP GUI with the type its methods expect.
    /// </para>
    /// </remarks>
    public class SapMethodCallModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the positional arguments; empty for methods without parameters.
        /// </summary>
        public IList<object> Arguments { get; set; } = [];

        /// <summary>
        /// Gets or sets the SAP GUI Scripting method name, such as <c>pressToolbarButton</c>.
        /// </summary>
        [Required]
        public string Method { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents the request body of the SAP-specific "set element property" endpoint.
    /// </summary>
    /// <remarks>
    /// The property must be on the driver's allow-list for the element's control family, such as <c>selectedRows</c>
    /// for a grid view shell or <c>verticalScrollbar.position</c> for a table control or scroll container. The value
    /// is a JSON scalar.
    /// </remarks>
    public class SapPropertyModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the property name, such as <c>selectedRows</c>; scroll bar positions use a dotted name.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the value to assign: a string, number, or Boolean.
        /// </summary>
        public object Value { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents the request body of the SAP-specific "element at point" endpoint.
    /// </summary>
    /// <remarks>
    /// The coordinates are screen pixels as SAP GUI reports them for <c>ScreenLeft</c> and <c>ScreenTop</c>.
    /// </remarks>
    public class SapPointModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the horizontal screen coordinate.
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Gets or sets the vertical screen coordinate.
        /// </summary>
        public int Y { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents the control found under a screen point, with an element reference the client can use at once.
    /// </summary>
    public class SapElementAtModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the W3C element reference identifier issued for the control.
        /// </summary>
        [JsonPropertyName(ElementProperties.ElementReference)]
        public string Element { get; set; }

        /// <summary>
        /// Gets or sets the inner-object description SAP GUI reports for the point, such as a grid cell; empty for the control itself.
        /// </summary>
        /// <remarks>
        /// SAP GUI Scripting does not report the geometry of inner objects, so <see cref="Rect"/> always frames the control.
        /// </remarks>
        public string InnerObject { get; set; }

        /// <summary>
        /// Gets or sets the control rectangle in screen pixels.
        /// </summary>
        public ElementRectModel Rect { get; set; }

        /// <summary>
        /// Gets or sets the absolute SAP GUI Scripting identifier of the control.
        /// </summary>
        public string SapId { get; set; }

        /// <summary>
        /// Gets or sets the SAP GUI Scripting type of the control, such as <c>GuiButton</c>.
        /// </summary>
        public string Type { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents the SAP status bar message of a session's main window.
    /// </summary>
    public class SapStatusBarModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets a value indicating whether the message is also shown in a popup window, such as <c>wnd[1]</c>.
        /// </summary>
        /// <remarks>
        /// SAP shows information and termination messages, and others when the user chose so, in a dialog box. The
        /// status bar still reports the message; this flag tells a client to look for the popup and answer it.
        /// </remarks>
        public bool MessageAsPopup { get; set; }

        /// <summary>
        /// Gets or sets the message class identifier, such as <c>00</c>.
        /// </summary>
        public string MessageId { get; set; }

        /// <summary>
        /// Gets or sets the message number within its class, such as <c>344</c>.
        /// </summary>
        public string MessageNumber { get; set; }

        /// <summary>
        /// Gets or sets the message type: S (success), W (warning), E (error), A (abort), I (information), or empty.
        /// </summary>
        public string MessageType { get; set; }

        /// <summary>
        /// Gets or sets the displayed message text.
        /// </summary>
        public string Text { get; set; }
        #endregion
    }
}
