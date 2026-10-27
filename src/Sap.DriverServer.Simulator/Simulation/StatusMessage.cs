namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Represents the message shown in a session's status bar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Messages use the simulator's own message class <c>ZSIM</c>, so a test can tell simulator messages from SAP ones.
    /// </para>
    /// <para>
    /// SAP shows information (<c>I</c>) and termination (<c>A</c>) messages in a dialog box, and success, warning,
    /// and error messages too when the user chose to see messages as popups. The status bar still carries such a
    /// message, with <c>MessageAsPopup</c> set, so a script knows to look for it in <c>wnd[1]</c>. The behavior follows
    /// the SAP GUI Scripting documentation; it has not been compared with a live SAP system.
    /// </para>
    /// </remarks>
    public sealed class StatusMessage
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies a termination (abort) message type, which ends the current transaction.
        /// </summary>
        public const string Abort = "A";

        /// <summary>
        /// Specifies an error message type.
        /// </summary>
        public const string Error = "E";

        /// <summary>
        /// Specifies an information message type, which SAP shows in a dialog box.
        /// </summary>
        public const string Information = "I";

        /// <summary>
        /// Specifies the message class of every simulator message.
        /// </summary>
        public const string MessageClass = "ZSIM";

        /// <summary>
        /// Specifies a success message type.
        /// </summary>
        public const string Success = "S";

        /// <summary>
        /// Specifies a warning message type.
        /// </summary>
        public const string Warning = "W";
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the empty message shown when nothing was reported.
        /// </summary>
        public static StatusMessage Empty { get; } = new();

        /// <summary>
        /// Gets a value indicating whether the message is shown in a popup (<c>wnd[1]</c>) rather than only in the
        /// status bar.
        /// </summary>
        public bool IsPopup { get; init; }

        /// <summary>
        /// Gets the message class; empty for the empty message.
        /// </summary>
        public string MessageId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the three-digit message number; empty for the empty message.
        /// </summary>
        public string MessageNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the message type: <see cref="Success"/>, <see cref="Warning"/>, <see cref="Error"/>,
        /// <see cref="Information"/>, <see cref="Abort"/>, or empty.
        /// </summary>
        public string MessageType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the message text.
        /// </summary>
        public string Text { get; init; } = string.Empty;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Creates a copy of the message that is shown in a popup.
        /// </summary>
        /// <returns>The copy, with <see cref="IsPopup"/> set.</returns>
        public StatusMessage ConvertToPopup()
        {
            // Copy every field and mark the copy, so the original status bar message stays unchanged.
            return new StatusMessage
            {
                IsPopup = true,
                MessageId = MessageId,
                MessageNumber = MessageNumber,
                MessageType = MessageType,
                Text = Text
            };
        }

        /// <summary>
        /// Creates a simulator message.
        /// </summary>
        /// <param name="messageType">The message type, such as <see cref="Success"/>.</param>
        /// <param name="number">The three-digit message number.</param>
        /// <param name="text">The message text.</param>
        /// <returns>The message, in the <see cref="MessageClass"/> class.</returns>
        public static StatusMessage NewMessage(string messageType, string number, string text)
        {
            // Use the simulator message class for every message.
            return new StatusMessage
            {
                MessageId = MessageClass,
                MessageNumber = number,
                MessageType = messageType,
                Text = text
            };
        }

        /// <summary>
        /// Tests whether a message type always opens a dialog box, as information and termination messages do.
        /// </summary>
        /// <param name="messageType">The message type, such as <see cref="Information"/>.</param>
        /// <returns>True for <see cref="Information"/> and <see cref="Abort"/>.</returns>
        public static bool TestPopupType(string messageType)
        {
            return messageType is Information or Abort;
        }
        #endregion
    }
}
