using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

using Sap.DriverServer.Simulator.Simulation;
using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's grid view shell (<c>GuiShell</c> with <c>SubType</c> <c>GridView</c>), the ALV grid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rows are zero-based and columns are addressed by SAP column name, which each grid column keeps in its
    /// <c>SortMemberPath</c>. Cell values are read through the column's binding, as strings.
    /// </para>
    /// <para>
    /// ALV grids (<see cref="AlvGrid"/>) add the toolbar, context menus, and hotspot clicks, and they load rows on
    /// demand: <c>getCellValue</c> returns an empty string for a row that has not been in view until a script sets
    /// <c>firstVisibleRow</c> to show it. <c>firstVisibleRow</c> and <c>visibleRowCount</c> work on every grid.
    /// </para>
    /// <para>
    /// A cell is the grid's inner object: <c>findByPosition</c> describes the cell under the point as
    /// <c>Cell(row,COLUMN)</c>, and <c>Visualize</c> frames the cell given in that form. The format is the simulator's
    /// model; it has not been compared with a real SAP GUI.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public sealed partial class GuiGridView : GuiShell
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiGridView"/> class.
        /// </summary>
        /// <param name="element">The data grid.</param>
        public GuiGridView(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the number of columns.
        /// </summary>
        public int ColumnCount => Grid.Columns.Count;

        /// <summary>
        /// Gets the column names in display order.
        /// </summary>
        public GuiCollection ColumnOrder => new(Grid.Columns.Select(i => i.SortMemberPath));

        /// <summary>
        /// Gets or sets the column name of the current cell; empty when there is no current cell.
        /// </summary>
        public string CurrentCellColumn
        {
            get => Grid.CurrentCell.Column?.SortMemberPath ?? string.Empty;
            set => SetCurrentCell(Math.Max(0, CurrentCellRow), value);
        }

        /// <summary>
        /// Gets or sets the zero-based row of the current cell; -1 when there is no current cell.
        /// </summary>
        public int CurrentCellRow
        {
            get => Grid.CurrentCell.Item is null ? -1 : Grid.Items.IndexOf(Grid.CurrentCell.Item);
            set => SetCurrentCell(value, CurrentCellColumn.Length == 0 ? Grid.Columns[0].SortMemberPath : CurrentCellColumn);
        }

        /// <summary>
        /// Gets or sets the zero-based row shown at the top of the grid.
        /// </summary>
        public int FirstVisibleRow
        {
            get => (int)(GetScrollHost()?.VerticalOffset ?? 0);
            set => ScrollToRow(value);
        }

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        public int RowCount => Grid.Items.Count;

        /// <summary>
        /// Gets or sets the selected rows as a comma-separated list of zero-based indexes, such as <c>0,2,3</c>.
        /// </summary>
        public string SelectedRows
        {
            get => string.Join(',', GetSelectedIndexes());
            set => SelectRows(value);
        }

        /// <inheritdoc />
        public override string SubType => "GridView";

        /// <summary>
        /// Gets the number of toolbar buttons; zero for grids without an ALV toolbar.
        /// </summary>
        public int ToolbarButtonCount => (Grid as AlvGrid)?.ToolbarButtons.Count ?? 0;

        /// <summary>
        /// Gets the number of rows the grid shows at once, partly visible rows excluded.
        /// </summary>
        public int VisibleRowCount
        {
            get
            {
                // Lay out first, so the count is right right after a resize or a screen change.
                Grid.UpdateLayout();

                return (int)(GetScrollHost()?.ViewportHeight ?? 0);
            }
        }

        // Gets the wrapped grid as an ALV grid, raising an error for plain grids without ALV functions.
        private AlvGrid Alv => Grid as AlvGrid ?? throw new SimulatorException($"The grid '{Id}' has no ALV toolbar or context menu.");

        // Gets the wrapped data grid.
        private DataGrid Grid => (DataGrid)Element;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Clears the row selection.
        /// </summary>
        public void ClearSelection()
        {
            // Clear the row selection; the current cell stays where it is.
            Grid.UnselectAll();
        }

        /// <summary>
        /// Clicks the current cell, which raises the hotspot event when the cell is in a hotspot column.
        /// </summary>
        public void ClickCurrentCell()
        {
            // Let the ALV grid decide; a cell outside the hotspot columns raises an error.
            Alv.ClickCurrentCell();
        }

        /// <summary>
        /// Opens the context menu of the current cell.
        /// </summary>
        public void ContextMenu()
        {
            // Open the cell menu; an item is chosen with selectContextMenuItem.
            Alv.OpenCellMenu();
        }

        /// <summary>
        /// Double-clicks the current cell, which runs the screen's double-click action for the row.
        /// </summary>
        public void DoubleClickCurrentCell()
        {
            // Raise the double click the grid screen handles for its current row.
            RaiseDoubleClick();
        }

        /// <summary>
        /// Gets a cell value.
        /// </summary>
        /// <param name="row">The zero-based row.</param>
        /// <param name="column">The column name, such as <c>PRICE</c>.</param>
        /// <returns>The value as displayed.</returns>
        public string GetCellValue(int row, string column)
        {
            // Read the bound property of the row item; the grid binds every column by property path.
            var item = GetItem(row);
            var path = ((Binding)((DataGridBoundColumn)GetColumn(column)).Binding).Path.Path;

            // An ALV grid knows only the rows that have been in view; others read as empty.
            if (Grid is AlvGrid alv && !alv.TestRowLoaded(row))
            {
                return string.Empty;
            }

            var value = item
                .GetType()
                .GetProperty(path)?
                .GetValue(item);

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <summary>
        /// Gets the function code of a toolbar button.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns>The function code, such as <c>&amp;REFRESH</c>.</returns>
        public string GetToolbarButtonId(int position)
        {
            return GetToolbarButton(position).Id;
        }

        /// <summary>
        /// Gets the text of a toolbar button.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns>The button text.</returns>
        public string GetToolbarButtonText(int position)
        {
            return GetToolbarButton(position).Text;
        }

        /// <summary>
        /// Gets the type of a toolbar button.
        /// </summary>
        /// <param name="position">The zero-based button position.</param>
        /// <returns><c>Button</c> for a plain button; <c>Menu</c> for a button that opens a menu.</returns>
        public string GetToolbarButtonType(int position)
        {
            return GetToolbarButton(position).Type;
        }

        /// <summary>
        /// Presses a plain toolbar button.
        /// </summary>
        /// <param name="buttonId">The button's function code, such as <c>&amp;REFRESH</c>.</param>
        public void PressToolbarButton(string buttonId)
        {
            // Run the button's function; menu buttons refuse this call.
            Alv.PressToolbarButton(buttonId, isContextButton: false);
        }

        /// <summary>
        /// Presses a menu toolbar button, which opens its menu.
        /// </summary>
        /// <param name="buttonId">The button's function code, such as <c>&amp;MB_EXPORT</c>.</param>
        public void PressToolbarContextButton(string buttonId)
        {
            // Open the button's menu; an item is chosen with selectContextMenuItem.
            Alv.PressToolbarButton(buttonId, isContextButton: true);
        }

        /// <summary>
        /// Selects every row.
        /// </summary>
        public void SelectAll()
        {
            // Select every row.
            Grid.SelectAll();
        }

        /// <summary>
        /// Selects an item of the open context menu by function code.
        /// </summary>
        /// <param name="functionCode">The item's function code, such as <c>&amp;XXL</c>.</param>
        public void SelectContextMenuItem(string functionCode)
        {
            // Run the item's function and close the menu; without an open menu the call fails.
            Alv.SelectMenuItem(functionCode);
        }

        /// <summary>
        /// Selects an item of the open context menu by text.
        /// </summary>
        /// <param name="text">The item text.</param>
        public void SelectContextMenuItemByText(string text)
        {
            // Run the item's function and close the menu; without an open menu the call fails.
            Alv.SelectMenuItemByText(text);
        }

        /// <summary>
        /// Makes a cell current and selects its row.
        /// </summary>
        /// <param name="row">The zero-based row.</param>
        /// <param name="column">The column name.</param>
        public void SetCurrentCell(int row, string column)
        {
            // Resolve both coordinates first, so an invalid one changes nothing.
            var item = GetItem(row);
            var gridColumn = GetColumn(column);

            Grid.CurrentCell = new DataGridCellInfo(item, gridColumn);
            Grid.SelectedItem = item;
            Grid.ScrollIntoView(item, gridColumn);
        }

        /// <inheritdoc />
        internal override string GetInnerObject(Point point)
        {
            // Find the cell under the point; headers and empty space have no inner object.
            var hit = Grid.InputHitTest(Grid.PointFromScreen(point)) as DependencyObject;
            while (hit is not null and not DataGridCell)
            {
                hit = VisualTreeHelper.GetParent(hit);
            }

            if (hit is not DataGridCell cell)
            {
                return string.Empty;
            }

            var row = DataGridRow.GetRowContainingElement(cell)?.GetIndex() ?? -1;

            return row < 0 ? string.Empty : NewCellDescription(row, cell.Column.SortMemberPath);
        }

        /// <inheritdoc />
        protected override FrameworkElement GetHighlightTarget(string innerObject)
        {
            // Frame the grid itself, or the cell named as Cell(row,COLUMN); other descriptions name nothing.
            if (innerObject.Length == 0)
            {
                return Element;
            }

            var match = CellPattern().Match(innerObject);
            if (!match.Success)
            {
                return null;
            }

            var row = int.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture);
            var column = match.Groups["column"].Value;

            return row < Grid.Items.Count && TestColumn(column) ? GetCellElement(row, column) : null;
        }

        // Matches an inner-object description of a cell, such as Cell(2,PRICE).
        [GeneratedRegex(@"^Cell\((?<row>\d+),(?<column>[^)]+)\)$")]
        private static partial Regex CellPattern();

        // Builds the inner-object description of a cell.
        private static string NewCellDescription(int row, string column)
        {
            return string.Create(CultureInfo.InvariantCulture, $"Cell({row},{column})");
        }

        // Gets the displayed cell element, scrolling the cell into view first so it is realized.
        private DataGridCell GetCellElement(int row, string column)
        {
            // Scroll and lay out, so the row container exists, then find the cell around the column's content.
            var item = GetItem(row);
            var gridColumn = GetColumn(column);
            Grid.ScrollIntoView(item, gridColumn);
            Grid.UpdateLayout();

            var container = (DataGridRow)Grid.ItemContainerGenerator.ContainerFromIndex(row);
            DependencyObject current = container is null ? null : gridColumn.GetCellContent(container);
            while (current is not null and not DataGridCell)
            {
                current = VisualTreeHelper.GetParent(current);
            }

            return current as DataGridCell;
        }

        // Gets a column by SAP name, raising an error that names it when the grid has no such column.
        private DataGridColumn GetColumn(string column)
        {
            // Match the SAP column name kept in the sort member path.
            return Grid.Columns.FirstOrDefault(i => string.Equals(i.SortMemberPath, column, StringComparison.Ordinal))
                ?? throw new SimulatorException($"The grid '{Id}' has no column '{column}'.");
        }

        // Gets a row item, raising an error that names the row when it is out of range.
        private object GetItem(int row)
        {
            // Reject rows outside the grid with a message that names the row.
            if (row < 0 || row >= Grid.Items.Count)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"The grid '{Id}' has no row {row}.");
                throw new SimulatorException(message);
            }

            return Grid.Items[row];
        }

        // Gets the template's scroll viewer; the grid scrolls by row, so its offsets count rows.
        private ScrollViewer GetScrollHost()
        {
            // Apply the template first, so the part exists for grids that were never shown.
            Grid.ApplyTemplate();

            return Grid.Template?.FindName("DG_ScrollViewer", Grid) as ScrollViewer;
        }

        // Gets the indexes of the selected rows in ascending order.
        private IEnumerable<int> GetSelectedIndexes()
        {
            // Map each selected item back to its row; the selection keeps click order, SAP reports row order.
            return Grid.SelectedItems
                .Cast<object>()
                .Select(Grid.Items.IndexOf)
                .Order();
        }

        // Gets a toolbar button by position, raising an error that names the position when it is out of range.
        private AlvGrid.ToolbarEntry GetToolbarButton(int position)
        {
            // Reject positions outside the toolbar with a message that names the position.
            var buttons = Alv.ToolbarButtons;
            if (position < 0 || position >= buttons.Count)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"The grid '{Id}' has no toolbar button at position {position}.");
                throw new SimulatorException(message);
            }

            return buttons[position];
        }

        // Scrolls a row to the top of the grid, which also loads it in an ALV grid.
        private void ScrollToRow(int row)
        {
            // Validate the row, scroll by row offset, and lay out so the new rows are realized and counted as loaded.
            _ = GetItem(row);
            GetScrollHost()?.ScrollToVerticalOffset(row);
            Grid.UpdateLayout();
        }

        // Selects rows from a list such as "0,2-4"; an empty list clears the selection.
        private void SelectRows(string rows)
        {
            // Expand single indexes and ranges into row items; invalid rows raise errors before anything changes.
            var items = new List<object>();

            foreach (var part in (rows ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var bounds = part.Split('-', StringSplitOptions.TrimEntries);
                var first = int.Parse(bounds[0], CultureInfo.InvariantCulture);
                var last = bounds.Length > 1 ? int.Parse(bounds[1], CultureInfo.InvariantCulture) : first;
                items.AddRange(Enumerable.Range(first, Math.Max(0, last - first + 1)).Select(GetItem));
            }

            // Single-selection grids, such as input help hit lists, take one row at most.
            if (Grid.SelectionMode == DataGridSelectionMode.Single)
            {
                if (items.Count > 1)
                {
                    throw new SimulatorException($"The grid '{Id}' allows one selected row only.");
                }

                Grid.SelectedItem = items.Count == 0 ? null : items[0];
                return;
            }

            // Replace the selection with the requested rows.
            Grid.UnselectAll();
            foreach (var item in items)
            {
                Grid.SelectedItems.Add(item);
            }
        }

        // Tests whether the grid has a column with the SAP name.
        private bool TestColumn(string column)
        {
            return Grid.Columns.Any(i => string.Equals(i.SortMemberPath, column, StringComparison.Ordinal));
        }
        #endregion
    }
}
