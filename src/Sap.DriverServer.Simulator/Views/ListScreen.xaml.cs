using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the list test lab (transaction <c>ZSIM_LIST</c>): a classic ABAP list of flights with a secondary list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list has no named fields. Each output field is a label named by its character position on the visible page,
    /// as in <c>usr/lbl[10,5]</c>. The traps it shows:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Scrolling, by the paging keys 80 to 83 or the user area's <c>verticalScrollbar.position</c>, makes the same label identifiers show other flights.</description></item>
    /// <item><description>Drilling down is done by placing the cursor on a line (<c>setFocus</c>) and pressing F2 (virtual key 2), which opens a secondary list.</description></item>
    /// <item><description>Back (F3) leaves the secondary list for the basic list at the same scroll position, not the transaction.</description></item>
    /// </list>
    /// <para>
    /// The rows are synthetic and reset whenever the transaction starts again.
    /// </para>
    /// </remarks>
    public partial class ListScreen : UserControl, IScrollableScreen, IVirtualKeyScreen
    {
        #region *** Constants    ***
        // Sets the width of one character cell in device-independent pixels.
        private const double CharWidth = 8;

        // Sets the first line that shows data; the lines above hold the title and the column headers.
        private const int FirstDataLine = 5;

        // Sets the height of one list line in device-independent pixels.
        private const double LineHeight = 17;

        // Sets the number of data lines one page shows.
        private const int ListPageSize = 18;

        // Sets the number of data rows.
        private const int RowCount = 120;
        #endregion

        #region *** Fields       ***
        // Holds the row shown by the secondary list; null while the basic list is shown.
        private FlightRow _detailRow;

        // Holds the zero-based row shown in the first data line of the basic list.
        private int _firstVisibleLine;

        // Holds the data rows.
        private readonly IList<FlightRow> _rows = FlightRow.NewRows(count: RowCount);

        // Reports drill-down problems in the status bar.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ListScreen"/> class.
        /// </summary>
        /// <param name="session">The session that reports drill-down problems.</param>
        public ListScreen(SimulatedSession session)
        {
            // Require the session, build the canvas, and show the first page of the basic list.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();
            ShowList();
        }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public int FirstVisibleLine => _detailRow is null ? _firstVisibleLine : 0;

        /// <inheritdoc />
        public int MaximumPosition => _detailRow is null ? RowCount - ListPageSize : 0;

        /// <inheritdoc />
        public int PageSize => ListPageSize;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void ScrollTo(int line)
        {
            // Scroll only the basic list; the secondary list has a single page.
            if (_detailRow is not null)
            {
                return;
            }

            _firstVisibleLine = Math.Clamp(line, 0, MaximumPosition);
            ShowList();
        }

        /// <inheritdoc />
        public void SendVirtualKey(int key)
        {
            // Back leaves the secondary list; F2 drills down; the paging keys move the basic list.
            switch (key)
            {
                case SimulatedSession.BackKey:
                    _detailRow = null;
                    ShowList();
                    break;
                case SimulatedSession.PickKey:
                    ShowDetail();
                    break;
                case SimulatedSession.FirstPageKey:
                    ScrollTo(0);
                    break;
                case SimulatedSession.PreviousPageKey:
                    ScrollTo(_firstVisibleLine - ListPageSize);
                    break;
                case SimulatedSession.NextPageKey:
                    ScrollTo(_firstVisibleLine + ListPageSize);
                    break;
                default:
                    ScrollTo(MaximumPosition);
                    break;
            }
        }

        /// <inheritdoc />
        public bool TestVirtualKey(int key)
        {
            // The secondary list takes Back; the basic list takes F2 and the paging keys.
            return _detailRow is not null
                ? key == SimulatedSession.BackKey
                : key is SimulatedSession.PickKey or SimulatedSession.FirstPageKey or SimulatedSession.PreviousPageKey or SimulatedSession.NextPageKey or SimulatedSession.LastPageKey;
        }

        // Places a label on the character grid and marks it as lbl[column,line]; data labels carry their row and take focus.
        private void AddLabel(int column, int line, string text, int row)
        {
            // Position by character cell, so the identifier and the screen position agree.
            var label = new TextBlock { Focusable = row >= 0, Tag = row, Text = text };
            if (row >= 0)
            {
                label.Foreground = Brushes.DarkBlue;
            }

            Canvas.SetLeft(label, column * CharWidth);
            Canvas.SetTop(label, line * LineHeight);
            ScriptingTree.SetId(label, string.Create(CultureInfo.InvariantCulture, $"lbl[{column},{line}]"));
            ListCanvas.Children.Add(label);
        }

        // Drills down on a double-clicked data label, like F2.
        private void OnListCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Focus the clicked label, as SAP places the cursor; a double click then picks its line.
            if (e.OriginalSource is not TextBlock { Focusable: true } label)
            {
                return;
            }

            label.Focus();
            if (e.ClickCount == 2)
            {
                e.Handled = true;
                ShowDetail();
            }
        }

        // Shows the secondary list of the row under the cursor, or reports that the cursor is not on a data line.
        private void ShowDetail()
        {
            // Find the focused label the way SAP finds the cursor position; only data labels carry a row.
            var focused = FocusManager.GetFocusedElement(Window.GetWindow(this)) as TextBlock;
            if (focused?.Tag is not int row || row < 0 || !ListCanvas.Children.Contains(focused))
            {
                _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Warning, "100", "Place the cursor on a list line."));
                return;
            }

            // Replace the page with the secondary list of the row.
            _detailRow = _rows[row];
            ListCanvas.Children.Clear();
            AddLabel(1, 1, "Flight details (secondary list)", row: -1);
            AddLabel(1, 3, "Airline", row: -1);
            AddLabel(20, 3, _detailRow.Airline, row: -1);
            AddLabel(1, 4, "Flight number", row: -1);
            AddLabel(20, 4, _detailRow.FlightNumber, row: -1);
            AddLabel(1, 5, "Date", row: -1);
            AddLabel(20, 5, _detailRow.FlightDate, row: -1);
            AddLabel(1, 6, "Price", row: -1);
            AddLabel(20, 6, $"{_detailRow.Price} {_detailRow.Currency}", row: -1);
            AddLabel(1, 8, "Press F3 to return to the basic list.", row: -1);
        }

        // Shows the basic list's current page: the title, the column headers, and one line per row.
        private void ShowList()
        {
            // Rebuild the page; labels of the previous page disappear, as after a server round trip.
            ListCanvas.Children.Clear();
            AddLabel(1, 1, "Flight list (simulated)", row: -1);
            AddLabel(1, 3, "Airline", row: -1);
            AddLabel(10, 3, "Flight", row: -1);
            AddLabel(20, 3, "Date", row: -1);
            AddLabel(34, 3, "Price", row: -1);
            AddLabel(46, 3, "Currency", row: -1);

            // Show the data lines of the page and a footer that names the visible range.
            for (var index = 0; index < ListPageSize; index++)
            {
                var row = _firstVisibleLine + index;
                var flight = _rows[row];
                var line = FirstDataLine + index;
                AddLabel(1, line, flight.Airline, row);
                AddLabel(10, line, flight.FlightNumber, row);
                AddLabel(20, line, flight.FlightDate, row);
                AddLabel(34, line, flight.Price, row);
                AddLabel(46, line, flight.Currency, row);
            }

            var footer = string.Create(CultureInfo.InvariantCulture, $"Lines {_firstVisibleLine + 1} to {_firstVisibleLine + ListPageSize} of {RowCount}");
            AddLabel(1, FirstDataLine + ListPageSize + 1, footer, row: -1);
        }
        #endregion
    }
}
