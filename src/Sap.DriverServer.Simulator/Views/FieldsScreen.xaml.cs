using System.Globalization;
using System.Linq;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the field attributes test lab (transaction <c>ZSIM_FIELDS</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The screen collects field attributes that scripts commonly trip over:
    /// </para>
    /// <list type="bullet">
    /// <item><description>A required entry field (<c>Required = true</c>), checked on Enter.</description></item>
    /// <item><description>Fields with an input help, opened by F4 (virtual key 4) while the field has the focus.</description></item>
    /// <item><description>A field whose maximum length cuts longer text that a script sets.</description></item>
    /// <item><description>A right-aligned numeric field (<c>Numerical = true</c>), checked on Enter.</description></item>
    /// <item><description>A padded output field, whose <c>Text</c> is trimmed while <c>DisplayedText</c> keeps the blanks.</description></item>
    /// <item><description>Output-only and disabled fields, which report <c>Changeable = false</c> and refuse text.</description></item>
    /// </list>
    /// <para>
    /// The behavior follows the SAP GUI Scripting documentation and scripting experience; it has not been compared
    /// with a live SAP system. The entries reset whenever the transaction starts again.
    /// </para>
    /// </remarks>
    public partial class FieldsScreen : UserControl, IScreenCheck
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="FieldsScreen"/> class.
        /// </summary>
        public FieldsScreen()
        {
            InitializeComponent();
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public StatusMessage ConfirmEntries()
        {
            // Require the company code first, as SAP checks required entries before any field content.
            if (CompanyCodeField.Text.Trim().Length == 0)
            {
                CompanyCodeField.Focus();

                return StatusMessage.NewMessage(StatusMessage.Error, "050", "Fill in all required entry fields.");
            }

            // Require a number in the amount field; the group separator and decimal point are accepted.
            if (!TestNumber(AmountField.Text))
            {
                AmountField.Focus();

                return StatusMessage.NewMessage(StatusMessage.Error, "051", "Enter a numeric value in the field Amount.");
            }

            return StatusMessage.NewMessage(StatusMessage.Success, "052", "Entries checked (simulated).");
        }

        // Tests whether a text is a number in the simulator's user format, such as 1,234.56 or -12.
        private static bool TestNumber(string text)
        {
            // Accept an empty field; otherwise require digits with optional sign, group separators, and decimal point.
            var value = text.Trim();

            return value.Length == 0 || (value.Any(char.IsAsciiDigit) && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _));
        }
        #endregion
    }
}
