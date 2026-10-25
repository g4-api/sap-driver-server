using System.ComponentModel.DataAnnotations;

namespace Sap.DriverServer.Models
{
    /// <summary>
    /// Represents the "Element Send Keys" request body used by G4 clients.
    /// </summary>
    /// <remarks>
    /// The UIA model also carries User32 keyboard options. SAP GUI Scripting sets control text directly,
    /// so the SAP model keeps only <see cref="Text"/> and ignores any extra members sent by clients.
    /// </remarks>
    public class TextInputModel
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the text written to the control; an empty string is accepted.
        /// </summary>
        [Required(AllowEmptyStrings = true)]
        public string Text { get; set; }
        #endregion
    }
}
