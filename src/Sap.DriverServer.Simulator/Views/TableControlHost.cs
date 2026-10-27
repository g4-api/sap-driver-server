using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows a dynpro table control (<c>GuiTableControl</c>): a fixed number of visible lines over a longer list of rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Like SAP's table control, the host creates cells for the visible lines only. A cell's identifier holds its
    /// column and its visible line, as in <c>txtITAB-MATNR[1,0]</c>, so the same identifier names another data row
    /// after the table scrolls, and rows outside the visible lines have no cells at all. Lines past the last row are
    /// empty and not changeable.
    /// </para>
    /// <para>
    /// Scrolling, by scroll bar, mouse wheel, paging keys, or <c>verticalScrollbar.position</c>, first writes the
    /// visible cells back to their rows, as the round trip in SAP does, and then shows the rows from the new first
    /// visible row. Row selection is kept per row, so it survives scrolling.
    /// </para>
    /// <para>
    /// The behavior follows the SAP GUI Scripting documentation and scripting experience; it has not been compared
    /// with a live SAP system.
    /// </para>
    /// </remarks>
    public sealed class TableControlHost : Border
    {
        #region *** Constants    ***
        // Selects the check box cell type and the value a selected check box stores.
        private const string CheckBoxPrefix = "chk";

        // Holds the value a selected check box cell stores, as SAP's 'X' flag.
        private const string CheckedValue = "X";

        // Sets the height of one table line in device-independent pixels.
        private const double LineHeight = 24;

        // Sets the number of lines one mouse wheel notch scrolls.
        private const int WheelLines = 3;
        #endregion

        #region *** Fields       ***
        /// <summary>
        /// Occurs after the table scrolled to a new first visible row.
        /// </summary>
        public event EventHandler Scrolled;

        // Holds the visible cells by line and column; the cells are the table's scripting children.
        private readonly FrameworkElement[,] _cells;

        // Holds the column definitions in display order.
        private readonly IReadOnlyList<TableColumn> _columns;

        // Records whether the cells are being loaded, so loading does not re-enter through the scroll bar's event.
        private bool _isLoading;

        // Holds every data row; the visible cells show a window of them.
        private readonly IReadOnlyList<TableRow> _rows;

        // Holds the scroll bar, which mirrors the first visible row.
        private readonly ScrollBar _scrollBar;

        // Holds the row selector buttons, one per visible line.
        private readonly ToggleButton[] _selectors;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="TableControlHost"/> class.
        /// </summary>
        /// <param name="columns">The column definitions in display order.</param>
        /// <param name="rows">The data rows; the host edits their values and selection in place.</param>
        /// <param name="visibleRowCount">The number of visible lines.</param>
        public TableControlHost(IReadOnlyList<TableColumn> columns, IReadOnlyList<TableRow> rows, int visibleRowCount)
        {
            // Assert the table contract before any element is created.
            AssertArguments(columns, rows, visibleRowCount);

            // Keep the columns and rows, and size the cell and selector arrays to the visible lines.
            _columns = columns;
            _rows = rows;
            VisibleRowCount = visibleRowCount;
            _cells = new FrameworkElement[visibleRowCount, columns.Count];
            _selectors = new ToggleButton[visibleRowCount];

            // Create the scroll bar over every row; a page is the visible line count.
            _scrollBar = new ScrollBar
            {
                LargeChange = visibleRowCount,
                Maximum = Math.Max(0, rows.Count - 1),
                Orientation = Orientation.Vertical,
                SmallChange = 1,
                ViewportSize = visibleRowCount
            };
            _scrollBar.ValueChanged += (_, e) => OnScrollBarValueChanged(e.NewValue);

            // Lay out the headers above the lines, with the scroll bar on the right, as SAP draws a table control.
            var lines = NewLinesGrid();
            var panel = new DockPanel();
            DockPanel.SetDock(_scrollBar, Dock.Right);
            panel.Children.Add(_scrollBar);
            var header = NewHeaderGrid();
            DockPanel.SetDock(header, Dock.Top);
            panel.Children.Add(header);
            panel.Children.Add(lines);

            // Frame the table and scroll it by mouse wheel too.
            Child = panel;
            BorderBrush = Brushes.SlateGray;
            BorderThickness = new Thickness(1);
            HorizontalAlignment = HorizontalAlignment.Left;
            PreviewMouseWheel += OnPreviewMouseWheel;

            // Show the first rows.
            LoadCells();

            return;

            // Asserts the columns, the rows, and the line count before the host builds its cells; nothing is mutated,
            // and an invalid argument fails the construction.
            static void AssertArguments(IReadOnlyList<TableColumn> columns, IReadOnlyList<TableRow> rows, int visibleRowCount)
            {
                // Require the definitions, the rows, and at least one visible line.
                ArgumentNullException.ThrowIfNull(argument: columns);
                ArgumentNullException.ThrowIfNull(argument: rows);
                ArgumentOutOfRangeException.ThrowIfLessThan(visibleRowCount, 1);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the column definitions in display order.
        /// </summary>
        public IReadOnlyList<TableColumn> Columns => _columns;

        /// <summary>
        /// Gets the zero-based index of the row shown in the first visible line.
        /// </summary>
        public int FirstVisibleRow { get; private set; }

        /// <summary>
        /// Gets the highest first visible row: the last row, so a script can scroll row by row to the end.
        /// </summary>
        public int MaximumPosition => Math.Max(0, _rows.Count - 1);

        /// <summary>
        /// Gets the number of data rows.
        /// </summary>
        public int RowCount => _rows.Count;

        /// <summary>
        /// Gets the number of visible lines.
        /// </summary>
        public int VisibleRowCount { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the cell element of a visible line.
        /// </summary>
        /// <param name="line">The zero-based visible line.</param>
        /// <param name="column">The zero-based column.</param>
        /// <returns>The cell element.</returns>
        /// <exception cref="SimulatorException">The line or the column is outside the visible cells.</exception>
        public FrameworkElement GetCell(int line, int column)
        {
            // Only visible lines have cells, as in SAP's table control.
            if (line < 0 || line >= VisibleRowCount || column < 0 || column >= _columns.Count)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"The table has no visible cell [{column},{line}].");
                throw new SimulatorException(message);
            }

            return _cells[line, column];
        }

        /// <summary>
        /// Gets a value indicating whether a data row is selected.
        /// </summary>
        /// <param name="row">The zero-based data row.</param>
        /// <returns>True when the row is selected.</returns>
        /// <exception cref="SimulatorException">The row does not exist.</exception>
        public bool GetRowSelected(int row)
        {
            return GetRow(row).IsSelected;
        }

        /// <summary>
        /// Scrolls the table so a data row is shown in the first visible line.
        /// </summary>
        /// <param name="position">The new first visible row, from 0 to <see cref="MaximumPosition"/>.</param>
        /// <exception cref="SimulatorException">The position is outside the scroll range.</exception>
        public void Scroll(int position)
        {
            // Reject positions outside the range with a message that names it.
            if (position < 0 || position > MaximumPosition)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"The scroll position {position} is outside 0 to {MaximumPosition}.");
                throw new SimulatorException(message);
            }

            // Keep the edits of the visible lines, then show the rows from the new position.
            StoreCells();
            FirstVisibleRow = position;
            LoadCells();
            Scrolled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Selects or clears a data row.
        /// </summary>
        /// <param name="row">The zero-based data row.</param>
        /// <param name="isSelected">True to select the row; false to clear it.</param>
        /// <exception cref="SimulatorException">The row does not exist.</exception>
        public void SetRowSelected(int row, bool isSelected)
        {
            // Change the row, then refresh its selector when the row is visible.
            GetRow(row).IsSelected = isSelected;
            var line = row - FirstVisibleRow;
            if (line >= 0 && line < VisibleRowCount)
            {
                _selectors[line].IsChecked = isSelected;
            }
        }

        // Creates the cell element of a column: a text box or a check box, marked with its scripting identifier.
        private static FrameworkElement NewCell(TableColumn column, int columnIndex, int line)
        {
            // Mark the cell with the column name and its [column,line] position, as SAP names table cells.
            var id = string.Create(CultureInfo.InvariantCulture, $"{column.Prefix}{column.Name}[{columnIndex},{line}]");
            FrameworkElement cell = column.Prefix == CheckBoxPrefix
                ? new CheckBox { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                : new TextBox
                {
                    Background = column.IsReadOnly ? Brushes.Gainsboro : Brushes.White,
                    IsReadOnly = column.IsReadOnly,
                    MaxLength = column.MaxLength,
                    TextAlignment = column.IsNumerical ? TextAlignment.Right : TextAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
            cell.Margin = new Thickness(1);
            ScriptingTree.SetId(cell, id);
            DynproField.SetIsNumerical(cell, column.IsNumerical);

            return cell;
        }

        // Gets a data row, raising an error that names it when it does not exist.
        private TableRow GetRow(int row)
        {
            // Reject rows outside the data with a message that names the row.
            if (row < 0 || row >= _rows.Count)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"The table has no row {row}.");
                throw new SimulatorException(message);
            }

            return _rows[row];
        }

        // Shows the rows from the first visible row in the cells; lines past the last row are empty and disabled.
        private void LoadCells()
        {
            // Block the scroll bar's event while the cells and the scroll bar change.
            _isLoading = true;
            try
            {
                for (var line = 0; line < VisibleRowCount; line++)
                {
                    // Show the line's row selection; a line past the last row has no row and cannot be selected.
                    var row = FirstVisibleRow + line < _rows.Count ? _rows[FirstVisibleRow + line] : null;
                    _selectors[line].IsChecked = row?.IsSelected == true;
                    _selectors[line].IsEnabled = row is not null;

                    // Fill the cells from the row; empty lines show nothing and refuse input.
                    for (var column = 0; column < _columns.Count; column++)
                    {
                        var value = row?.Values[column] ?? string.Empty;
                        var cell = _cells[line, column];
                        cell.IsEnabled = row is not null;

                        if (cell is CheckBox checkBox)
                        {
                            checkBox.IsChecked = value == CheckedValue;
                        }
                        else
                        {
                            ((TextBox)cell).Text = value;
                        }
                    }
                }

                _scrollBar.Value = FirstVisibleRow;
            }
            finally
            {
                _isLoading = false;
            }
        }

        // Builds the header line: one title per column above the cells, after the selector column.
        private Grid NewHeaderGrid()
        {
            // Mirror the lines grid's columns, so titles sit above their cells.
            var header = new Grid { Height = LineHeight };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });

            for (var index = 0; index < _columns.Count; index++)
            {
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_columns[index].Width) });
                var title = new TextBlock
                {
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(4, 0, 0, 0),
                    Text = _columns[index].Title,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(title, index + 1);
                header.Children.Add(title);
            }

            return header;
        }

        // Builds the visible lines: a selector button followed by one cell per column.
        private Grid NewLinesGrid()
        {
            // Define the selector column and one column per table column.
            var lines = new Grid();
            lines.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            foreach (var column in _columns)
            {
                lines.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(column.Width) });
            }

            // Create each line's selector and cells; only these lines exist for scripting.
            for (var line = 0; line < VisibleRowCount; line++)
            {
                lines.RowDefinitions.Add(new RowDefinition { Height = new GridLength(LineHeight) });
                var lineIndex = line;
                var selector = new ToggleButton { Margin = new Thickness(2), ToolTip = "Select row" };
                selector.Click += (_, _) => GetRow(FirstVisibleRow + lineIndex).IsSelected = selector.IsChecked == true;
                Grid.SetRow(selector, line);
                lines.Children.Add(selector);
                _selectors[line] = selector;

                for (var column = 0; column < _columns.Count; column++)
                {
                    var cell = NewCell(_columns[column], column, line);
                    Grid.SetRow(cell, line);
                    Grid.SetColumn(cell, column + 1);
                    lines.Children.Add(cell);
                    _cells[line, column] = cell;
                }
            }

            return lines;
        }

        // Scrolls by mouse wheel, three lines per notch, within the scroll range.
        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Move against the wheel direction and clamp to the range, as list controls do.
            e.Handled = true;
            var position = Math.Clamp(FirstVisibleRow - (Math.Sign(e.Delta) * WheelLines), 0, MaximumPosition);
            if (position != FirstVisibleRow)
            {
                Scroll(position);
            }
        }

        // Scrolls to the scroll bar's new value, unless the value changed because the cells were loaded.
        private void OnScrollBarValueChanged(double value)
        {
            // Ignore the change made by loading; user drags scroll to the nearest whole row.
            var position = (int)Math.Round(value);
            if (!_isLoading && position != FirstVisibleRow)
            {
                Scroll(position);
            }
        }

        // Writes the visible cells back to their rows, as SAP transports the table lines on every round trip.
        private void StoreCells()
        {
            // Store only lines that show a row; empty lines past the end hold no data.
            for (var line = 0; line < VisibleRowCount && FirstVisibleRow + line < _rows.Count; line++)
            {
                // Read each cell back in its column's format: X or empty for check boxes, the text otherwise.
                var row = _rows[FirstVisibleRow + line];
                for (var column = 0; column < _columns.Count; column++)
                {
                    row.Values[column] = _cells[line, column] switch
                    {
                        CheckBox checkBox => checkBox.IsChecked == true ? CheckedValue : string.Empty,
                        TextBox textBox => textBox.Text,
                        _ => row.Values[column]
                    };
                }
            }
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Describes one table column: its SAP name, title, cell type, and input attributes.
        /// </summary>
        public sealed class TableColumn
        {
            #region *** Properties   ***
            /// <summary>
            /// Gets a value indicating whether the column holds numbers, which are right-aligned.
            /// </summary>
            public bool IsNumerical { get; init; }

            /// <summary>
            /// Gets a value indicating whether the column's cells are output only.
            /// </summary>
            public bool IsReadOnly { get; init; }

            /// <summary>
            /// Gets the maximum text length of the column's cells; zero when unlimited.
            /// </summary>
            public int MaxLength { get; init; }

            /// <summary>
            /// Gets the SAP field name, such as <c>ITAB-MATNR</c>.
            /// </summary>
            public string Name { get; init; }

            /// <summary>
            /// Gets the identifier prefix of the cells: <c>txt</c>, <c>ctxt</c>, or <c>chk</c>.
            /// </summary>
            public string Prefix { get; init; }

            /// <summary>
            /// Gets the column title shown in the header.
            /// </summary>
            public string Title { get; init; }

            /// <summary>
            /// Gets the column width in device-independent pixels.
            /// </summary>
            public double Width { get; init; }
            #endregion
        }

        /// <summary>
        /// Holds one data row: one value per column and the row selection.
        /// </summary>
        /// <remarks>
        /// Check box columns store <c>X</c> when selected and an empty string otherwise.
        /// </remarks>
        public sealed class TableRow
        {
            #region *** Properties   ***
            /// <summary>
            /// Gets or sets a value indicating whether the row is selected.
            /// </summary>
            public bool IsSelected { get; set; }

            /// <summary>
            /// Gets the values in column order; the host writes edits back into this array.
            /// </summary>
            public IList<string> Values { get; init; }
            #endregion
        }
        #endregion
    }
}
