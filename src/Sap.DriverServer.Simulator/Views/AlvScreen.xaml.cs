using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the ALV grid test lab (transaction <c>ZSIM_ALV</c>): 500 flights in an ALV grid with a toolbar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grid is <c>usr/cntlALV_CONTAINER/shellcont/shell</c>. The traps it shows:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Rows load on demand: <c>getCellValue</c> reads an empty string for a row that was never in view.</description></item>
    /// <item><description>Toolbar buttons are pressed by function code; the export button is a menu button, pressed with <c>pressToolbarContextButton</c>.</description></item>
    /// <item><description>Context menus wait for <c>selectContextMenuItem</c>.</description></item>
    /// <item><description>Sorting reorders the rows, so a row index names another flight afterwards.</description></item>
    /// <item><description>The flight number column is a hotspot (<c>clickCurrentCell</c>); a double click opens a detail popup.</description></item>
    /// </list>
    /// <para>
    /// The rows are synthetic and reset whenever the transaction starts again. No function writes files or uses the
    /// clipboard; the export and copy functions only report what they would do.
    /// </para>
    /// </remarks>
    public partial class AlvScreen : UserControl
    {
        #region *** Constants    ***
        // Sets the number of rows; enough that most rows start unloaded.
        private const int RowCount = 500;

        // Lists the cell context menu items.
        private static readonly AlvGrid.MenuEntry[] CellMenu =
        [
            new() { Id = "&COPY", Text = "Copy Text" },
            new() { Id = "&DETAIL", Text = "Details" }
        ];

        // Lists the toolbar buttons; the export button opens a menu.
        private static readonly AlvGrid.ToolbarEntry[] ToolbarButtons =
        [
            new() { Id = "&DETAIL", Text = "Details" },
            new() { Id = "&SORT_ASC", Text = "Sort Ascending" },
            new() { Id = "&SORT_DSC", Text = "Sort Descending" },
            new() { Id = "&REFRESH", Text = "Refresh" },
            new()
            {
                Id = "&MB_EXPORT",
                Menu =
                [
                    new() { Id = "&XXL", Text = "Spreadsheet..." },
                    new() { Id = "&PC", Text = "Local File..." }
                ],
                Text = "Export"
            }
        ];
        #endregion

        #region *** Fields       ***
        // Holds the rows in their original order; Refresh restores it.
        private readonly IList<FlightRow> _rows = FlightRow.NewRows(count: RowCount);

        // Shows the function results and opens the detail popup.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="AlvScreen"/> class.
        /// </summary>
        /// <param name="session">The session that shows results and popups.</param>
        public AlvScreen(SimulatedSession session)
        {
            // Require the session that reports the functions, then build the screen.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();

            // Configure the ALV functions and show the rows in their original order.
            FlightGrid.CellMenu = CellMenu;
            FlightGrid.FunctionHandler = RunFunction;
            FlightGrid.HotspotColumns = ["CONNID"];
            FlightGrid.ToolbarButtons = ToolbarButtons;
            FlightGrid.ItemsSource = _rows.ToList();

            // Draw one button per toolbar entry; menu buttons open their menu like pressToolbarContextButton.
            foreach (var entry in ToolbarButtons)
            {
                var button = new Button
                {
                    Content = entry.Menu.Count > 0 ? entry.Text + " ▾" : entry.Text,
                    Margin = new Thickness(0, 0, 4, 0),
                    Padding = new Thickness(8, 2, 8, 2),
                    ToolTip = entry.Id
                };
                button.Click += (_, _) => FlightGrid.PressToolbarButton(entry.Id, isContextButton: entry.Menu.Count > 0);
                ToolbarPanel.Children.Add(button);
            }
        }
        #endregion

        #region *** Methods      ***
        // Opens the detail popup for the current row, or warns when there is none.
        private void OpenDetails()
        {
            // Use the current row, as SAP's detail function does; without one, report what is missing.
            if (FlightGrid.CurrentItem is not FlightRow row)
            {
                _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Warning, "070", "Select a row first."));
                return;
            }

            _session.OpenPopup(new RowDetailPopup(_session, row));
        }

        // Opens the detail popup for the double-clicked row; double clicks outside a row change nothing.
        private void OnFlightGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Show details only for a current row.
            if (FlightGrid.CurrentItem is FlightRow)
            {
                e.Handled = true;
                OpenDetails();
            }
        }

        // Runs a function code from the toolbar, a menu, or a hotspot click.
        private void RunFunction(string functionCode)
        {
            // Dispatch by function code; every function reports its result in the status bar.
            var current = FlightGrid.CurrentItem as FlightRow;
            switch (functionCode)
            {
                case "&DETAIL":
                    OpenDetails();
                    break;
                case "&SORT_ASC" or "&SORT_DSC":
                    SortRows(isDescending: functionCode == "&SORT_DSC");
                    break;
                case "&REFRESH":
                    FlightGrid.ItemsSource = _rows.ToList();
                    _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "072", "List refreshed."));
                    break;
                case "&XXL" or "&PC":
                    var target = functionCode == "&XXL" ? "a spreadsheet" : "a local file";
                    _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "073", $"Export to {target} (simulated, nothing written)."));
                    break;
                case "&COPY":
                    var text = current is null ? string.Empty : current.GetValue(FlightGrid.CurrentCell.Column?.SortMemberPath ?? "CARRID");
                    _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "074", $"Copied '{text}' (simulated, clipboard unchanged)."));
                    break;
                case AlvGrid.HotspotFunction:
                    _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "075", $"Hotspot: flight {current?.Airline} {current?.FlightNumber}."));
                    break;
                default:
                    _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Warning, "079", $"Function {functionCode} is not implemented."));
                    break;
            }
        }

        // Sorts the rows by the current column, or by airline without a current cell, and reports the column.
        private void SortRows(bool isDescending)
        {
            // Sort a copy by the column's value; ordinal order keeps the result the same on every machine.
            var column = FlightGrid.CurrentCell.Column?.SortMemberPath ?? "CARRID";
            var sorted = isDescending
                ? _rows.OrderByDescending(i => i.GetValue(column), StringComparer.Ordinal).ToList()
                : _rows.OrderBy(i => i.GetValue(column), StringComparer.Ordinal).ToList();

            // Replace the rows; the grid forgets which rows it loaded, as an ALV refresh does.
            FlightGrid.ItemsSource = sorted;
            var direction = isDescending ? "descending" : "ascending";
            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "071", $"Sorted by {column}, {direction}."));
        }
        #endregion
    }
}
