using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows an input help (F4) hit list: the entries of one input help in a grid view shell.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grid is <c>usr/cntlRESULTLIST/shellcont/shell</c>. A script selects an entry through <c>selectedRows</c>
    /// or <c>currentCellRow</c> and copies it with Enter or <c>tbar[0]/btn[0]</c>; <c>doubleClickCurrentCell</c> copies
    /// the current entry at once. The key of the entry is written into the field that opened the input help.
    /// </para>
    /// <para>
    /// Copying without a selected entry keeps the popup open and reports a warning in the main window's status bar.
    /// </para>
    /// </remarks>
    public partial class ValueHelpPopup : UserControl, IPopupScreen
    {
        #region *** Fields       ***
        // Closes the popup and reports a missing selection.
        private readonly SimulatedSession _session;

        // Receives the key of the copied entry.
        private readonly TextBox _target;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ValueHelpPopup"/> class.
        /// </summary>
        /// <param name="session">The session that closes the popup.</param>
        /// <param name="target">The field that opened the input help and receives the copied key.</param>
        /// <param name="valueHelp">The input help whose entries are listed.</param>
        public ValueHelpPopup(SimulatedSession session, TextBox target, ValueHelpCatalog.ValueHelp valueHelp)
        {
            // Assert the constructor contract before any content is built.
            AssertArguments(session, target, valueHelp);

            // Keep the session and the target field, then build the content.
            _session = session;
            _target = target;
            InitializeComponent();

            // Build one column per input help column; each binds a row property and keeps its SAP name for scripting.
            string[] paths = [nameof(ValueHelpCatalog.ValueHelpRow.Key), nameof(ValueHelpCatalog.ValueHelpRow.Text), nameof(ValueHelpCatalog.ValueHelpRow.Extra)];
            for (var index = 0; index < paths.Length; index++)
            {
                ResultGrid.Columns.Add(new DataGridTextColumn
                {
                    Binding = new Binding(paths[index]),
                    Header = valueHelp.Headers[index],
                    SortMemberPath = valueHelp.Columns[index],
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star)
                });
            }

            // Fill the grid and title the popup like SAP's hit list, with the number of entries found.
            ResultGrid.ItemsSource = valueHelp.Rows;
            Title = string.Create(CultureInfo.InvariantCulture, $"{valueHelp.Title}: {valueHelp.Rows.Count} Entries found");

            return;

            // Asserts the session, the target field, and the input help before the popup is built; nothing is
            // mutated, and a missing argument fails the construction with an ArgumentNullException.
            static void AssertArguments(SimulatedSession session, TextBox target, ValueHelpCatalog.ValueHelp valueHelp)
            {
                // Require the session that closes the popup, the field that receives the key, and the listed help.
                ArgumentNullException.ThrowIfNull(argument: session);
                ArgumentNullException.ThrowIfNull(argument: target);
                ArgumentNullException.ThrowIfNull(argument: valueHelp);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public string ConfirmText => "Copy";

        /// <inheritdoc />
        public bool IsCancelable => true;

        /// <inheritdoc />
        public string Title { get; }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void Confirm()
        {
            // Copy the selected entry, or the current one after a double click; without either, keep the popup open.
            if ((ResultGrid.SelectedItem ?? ResultGrid.CurrentItem) is not ValueHelpCatalog.ValueHelpRow row)
            {
                _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Warning, "060", "Select an entry first."));
                return;
            }

            // Write the key into the field, then close the popup without changing the status bar.
            _target.Text = row.Key;
            _session.CompletePopup(message: null);
        }

        // Copies the double-clicked entry, as SAP's hit list does.
        private void OnResultGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Copy the current entry; a double click outside the rows finds no entry and only warns.
            e.Handled = true;
            Confirm();
        }
        #endregion
    }
}
