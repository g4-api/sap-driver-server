using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the server-side state of one WebDriver session attached to a SAP GUI session.
    /// </summary>
    /// <remarks>
    /// The model holds identifiers only, never COM references. Every request re-resolves SAP objects from
    /// <see cref="SapSessionPath"/> and the cached SAP identifiers, so no COM object outlives the request that
    /// created it and a closed SAP window surfaces as a stale reference instead of a dead RCW.
    /// </remarks>
    public class SapSessionModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the capabilities the session was created with.
        /// </summary>
        public IDictionary<string, object> Capabilities { get; set; }

        /// <summary>
        /// Gets the element references issued for this session, keyed by element reference identifier.
        /// </summary>
        /// <remarks>
        /// The cache is cleared when the session is deleted. One SAP identifier maps to one reference, so
        /// repeated lookups of the same control return the same element reference.
        /// </remarks>
        [JsonIgnore]
        public ConcurrentDictionary<string, SapElementModel> Elements { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Gets or sets the SAP GUI Scripting identifier of the attached session, such as <c>/app/con[0]/ses[0]</c>.
        /// </summary>
        public string SapSessionPath { get; set; }

        /// <summary>
        /// Gets or sets the WebDriver session identifier.
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the SAP window name selected through "Switch To Window", such as <c>wnd[1]</c>.
        /// </summary>
        /// <remarks>
        /// Null or empty means "use the session's active window" for window, screenshot, and virtual key operations.
        /// </remarks>
        public string WindowName { get; set; }
        #endregion
    }

    /// <summary>
    /// Represents one element reference issued to a client.
    /// </summary>
    public class SapElementModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the W3C element reference identifier returned to the client.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the absolute SAP GUI Scripting identifier the reference resolves through <c>findById</c>.
        /// </summary>
        public string SapId { get; set; }

        /// <summary>
        /// Gets or sets the SAP GUI Scripting type observed when the reference was issued, such as <c>GuiButton</c>.
        /// </summary>
        public string Type { get; set; }
        #endregion
    }
}
