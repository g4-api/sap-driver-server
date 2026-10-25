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
