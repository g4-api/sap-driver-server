using System.Windows.Controls;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the logon screen: client, user, password, and language.
    /// </summary>
    /// <remarks>
    /// Any user and password are accepted. The password never leaves the password box: the screen only checks that
    /// one was entered and clears it in the same step.
    /// </remarks>
    public partial class LogonScreen : UserControl
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="LogonScreen"/> class.
        /// </summary>
        public LogonScreen()
        {
            InitializeComponent();
        }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Checks that client, user, and password were entered, and clears the password in every case.
        /// </summary>
        /// <param name="client">The entered client; empty when the logon is incomplete.</param>
        /// <returns>True when every required field has a value.</returns>
        public bool TryTakeLogon(out string client)
        {
            // Check the password's length without reading its text, then clear it whatever the outcome.
            bool hasPassword;
            using (var password = PasswordField.SecurePassword)
            {
                hasPassword = password.Length > 0;
            }

            PasswordField.Clear();

            // Require the client and the user as well; the user name is not kept.
            var isComplete = hasPassword && ClientField.Text.Trim().Length > 0 && UserField.Text.Trim().Length > 0;
            client = isComplete ? ClientField.Text.Trim() : string.Empty;

            return isComplete;
        }
        #endregion
    }
}
