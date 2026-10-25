namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Represents the message shown in a session's status bar.
    /// </summary>
    /// <remarks>
    /// Messages use the simulator's own message class <c>ZSIM</c>, so a test can tell simulator messages from SAP ones.
    /// </remarks>
    public sealed class StatusMessage
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies an error message type.
        /// </summary>
        public const string Error = "E";

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
        /// Gets the message class; empty for the empty message.
        /// </summary>
        public string MessageId { get; init; } = string.Empty;

        /// <summary>
        /// Gets the three-digit message number; empty for the empty message.
        /// </summary>
        public string MessageNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the message type: <see cref="Success"/>, <see cref="Warning"/>, <see cref="Error"/>, or empty.
        /// </summary>
        public string MessageType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the message text.
        /// </summary>
        public string Text { get; init; } = string.Empty;
        #endregion

        #region *** Methods      ***
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
        #endregion
    }
}
