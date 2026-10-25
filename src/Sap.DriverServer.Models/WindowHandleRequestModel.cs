using System.ComponentModel.DataAnnotations;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the W3C "Switch To Window" request body.
    /// </summary>
    public class WindowHandleRequestModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the target window: a hexadecimal window handle as returned by the window endpoints,
        /// a SAP window name such as <c>wnd[1]</c>, or an exact window title.
        /// </summary>
        [Required]
        public string Handle { get; set; }
        #endregion
    }
}
