using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the W3C WebDriver "Execute Script" request body.
    /// </summary>
    /// <remarks>
    /// The model is kept for contract parity with the UIA driver server. The SAP driver does not execute
    /// scripts and answers every execute request with an "unsupported operation" error.
    /// </remarks>
    public class ScriptInputModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the script arguments.
        /// </summary>
        public IEnumerable<object> Parameters { get; set; }

        /// <summary>
        /// Gets or sets the script source text.
        /// </summary>
        [Required]
        public string Script { get; set; }
        #endregion
    }
}
