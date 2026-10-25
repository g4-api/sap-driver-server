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
        /// Double-clicks the current cell, which reports the row in the status bar.
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

            var value = item
                .GetType()
                .GetProperty(path)?
                .GetValue(item);

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
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

        // Gets the indexes of the selected rows in ascending order.
        private IEnumerable<int> GetSelectedIndexes()
        {
            // Map each selected item back to its row; the selection keeps click order, SAP reports row order.
            return Grid.SelectedItems
                .Cast<object>()
                .Select(Grid.Items.IndexOf)
                .Order();
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
