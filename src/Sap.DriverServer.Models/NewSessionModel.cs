/*
 * RESOURCES
 * https://www.w3.org/TR/webdriver/#new-session
 */
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the W3C WebDriver "New Session" request body.
    /// </summary>
    /// <remarks>
    /// The shape is ported unchanged from the UIA driver server. SAP session selection reads the
    /// <see cref="SapCapabilities.Options"/> entry from the merged capabilities.
    /// </remarks>
    public class NewSessionModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the requested capabilities.
        /// </summary>
        [Required]
        public CapabilitiesModel Capabilities { get; set; }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Represents the W3C capabilities negotiation object.
        /// </summary>
        public class CapabilitiesModel
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="CapabilitiesModel"/> class with empty match collections.
            /// </summary>
            public CapabilitiesModel()
            {
                // Start with stable empty collections so callers that omit either section never observe null.
                AlwaysMatch = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                FirstMatch = [];
            }

            /// <summary>
            /// Gets or sets the capabilities that every match must satisfy.
            /// </summary>
            public Dictionary<string, object> AlwaysMatch { get; set; }

            /// <summary>
            /// Gets or sets the alternative capability sets; the server uses the first set when
            /// <see cref="AlwaysMatch"/> is empty.
            /// </summary>
            public Dictionary<string, object>[] FirstMatch { get; set; }
        }
        #endregion
    }
}
