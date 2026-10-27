using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the table control test lab (transaction <c>ZSIM_TABLE</c>): 200 item rows in a table control with 12
    /// visible lines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table is <c>usr/tblSAPMZSIM_TABLETC_ITEMS</c>, with the columns item (output only), material (input
    /// help field), quantity (numeric), and done (check box). The traps it shows:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Cells exist only for the visible lines; <c>[column,line]</c> counts lines, not rows.</description></item>
    /// <item><description><c>RowCount</c> (200) differs from <c>VisibleRowCount</c> (12).</description></item>
    /// <item><description>Scrolling through <c>verticalScrollbar.position</c> or the paging keys 80 to 83 changes what each cell identifier shows.</description></item>
    /// <item><description>Lines past the last row are empty and not changeable.</description></item>
    /// </list>
    /// <para>
    /// The rows are synthetic and reset whenever the transaction starts again.
    /// </para>
    /// </remarks>
    public partial class TableScreen : UserControl, IVirtualKeyScreen
    {
        #region *** Constants    ***
        // Sets the number of data rows.
        private const int RowCount = 200;

        // Identifies the table control, as SAP names it from program and control: tbl<program><control>.
        private const string TableId = "tblSAPMZSIM_TABLETC_ITEMS";

        // Sets the number of visible lines.
        private const int VisibleRowCount = 12;

        // Lists the columns: item, material, quantity, and done.
        private static readonly TableControlHost.TableColumn[] Columns =
        [
            new() { IsReadOnly = true, Name = "ITAB-POSNR", Prefix = "txt", Title = "Item", Width = 80 },
            new() { MaxLength = 18, Name = "ITAB-MATNR", Prefix = "ctxt", Title = "Material", Width = 170 },
            new() { IsNumerical = true, MaxLength = 13, Name = "ITAB-MENGE", Prefix = "txt", Title = "Quantity", Width = 100 },
            new() { Name = "ITAB-DONE", Prefix = "chk", Title = "Done", Width = 60 }
        ];
        #endregion

        #region *** Fields       ***
        // Holds the table control, which owns the rows and the visible cells.
        private readonly TableControlHost _table;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="TableScreen"/> class.
        /// </summary>
        public TableScreen()
        {
            InitializeComponent();

            // Build the table over fresh synthetic rows and mark it for scripting.
            _table = new TableControlHost(Columns, NewRows(), VisibleRowCount);
            ScriptingTree.SetId(_table, TableId);
            _table.Scrolled += (_, _) => ShowPosition();
            TableArea.Content = _table;
            ShowPosition();
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void SendVirtualKey(int key)
        {
            // Page by the visible line count; the first and last page keys jump to the ends.
            var position = key switch
            {
                SimulatedSession.FirstPageKey => 0,
                SimulatedSession.PreviousPageKey => _table.FirstVisibleRow - VisibleRowCount,
                SimulatedSession.NextPageKey => _table.FirstVisibleRow + VisibleRowCount,
                _ => RowCount - VisibleRowCount
            };

            _table.Scroll(Math.Clamp(position, 0, _table.MaximumPosition));
        }

        /// <inheritdoc />
        public bool TestVirtualKey(int key)
        {
            return key is SimulatedSession.FirstPageKey or SimulatedSession.PreviousPageKey or SimulatedSession.NextPageKey or SimulatedSession.LastPageKey;
        }

        // Creates the synthetic rows: item numbers in steps of 10, numbered materials, small quantities, and every fifth done.
        private static List<TableControlHost.TableRow> NewRows()
        {
            return Enumerable
                .Range(0, RowCount)
                .Select(i => new TableControlHost.TableRow
                {
                    Values =
                    [
                        string.Create(CultureInfo.InvariantCulture, $"{(i + 1) * 10:000000}"),
                        string.Create(CultureInfo.InvariantCulture, $"SIM-MAT-{i + 1:0000}"),
                        string.Create(CultureInfo.InvariantCulture, $"{(i % 7) + 1}"),
                        i % 5 == 0 ? "X" : string.Empty
                    ]
                })
                .ToList();
        }

        // Shows which rows the visible lines hold, as the entry counter below SAP tables does.
        private void ShowPosition()
        {
            // Report one-based row numbers; the last page may show fewer rows than lines.
            var first = _table.FirstVisibleRow + 1;
            var last = Math.Min(_table.FirstVisibleRow + VisibleRowCount, RowCount);
            PositionText.Text = string.Create(CultureInfo.InvariantCulture, $"Entries {first} to {last} of {RowCount}");
        }
        #endregion
    }
}
