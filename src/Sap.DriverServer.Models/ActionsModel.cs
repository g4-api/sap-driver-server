/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#actions
 */
using System.Collections.Generic;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the W3C WebDriver "Perform Actions" request body.
    /// </summary>
    /// <remarks>
    /// The shape is ported unchanged from the UIA driver server so G4 clients send one payload to both servers.
    /// The SAP driver only executes key sources whose keys map to SAP virtual keys; other sources are rejected
    /// with an "unsupported operation" error instead of being emulated through raw input.
    /// </remarks>
    public class ActionsModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the input sources, each carrying its own ordered list of action items.
        /// </summary>
        public IEnumerable<ActionModel> Actions { get; set; }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents one W3C input source, such as a "key" or "pointer" source.
        /// </summary>
        public class ActionModel
        {
            /// <summary>
            /// Gets or sets the ordered action items; each item is a raw dictionary such as
            /// <c>{"type":"keyDown","value":""}</c>.
            /// </summary>
            public IEnumerable<Dictionary<string, object>> Actions { get; set; }

            /// <summary>
            /// Gets or sets the client-assigned identifier of the input source.
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// Gets or sets the input source type: "key", "pointer", "wheel", or "none".
            /// </summary>
            public string Type { get; set; }
        }
        #endregion
    }
}
