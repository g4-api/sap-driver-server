using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiTableControl</c>: a dynpro table control (<c>tbl</c>) with visible lines over more rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The children are the cells of the visible lines only, named <c>txtITAB-MATNR[column,line]</c>, where the line is
    /// relative to the first visible row. <c>RowCount</c> counts every row, <c>VisibleRowCount</c> only the lines;
    /// <c>verticalScrollbar.position</c> moves the first visible row, after which the same cell identifiers show other
    /// rows. <c>getCell</c> takes a visible line too, while <c>getAbsoluteRow</c> takes a data row.
    /// </para>
    /// <para>
    /// The behavior follows the SAP GUI Scripting documentation and scripting experience; it has not been compared
    /// with a live SAP system.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiTableControl : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTableControl"/> class.
        /// </summary>
        /// <param name="element">The table control host.</param>
        public GuiTableControl(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the columns in display order.
        /// </summary>
        public GuiCollection Columns => new(Table.Columns.Select(i => new GuiTableColumn(i.Name, i.Title)));

        /// <summary>
        /// Gets the number of data rows, visible or not.
        /// </summary>
        public int RowCount => Table.RowCount;

        /// <summary>
        /// Gets the rows of the visible lines, first visible row first; lines past the last row are not selectable.
        /// </summary>
        public GuiCollection Rows => new(Enumerable.Range(Table.FirstVisibleRow, Table.VisibleRowCount).Select(i => new GuiTableRow(Table, i)));

        /// <summary>
        /// Gets the vertical scroll bar, whose position is the first visible row.
        /// </summary>
        public GuiScrollbar VerticalScrollbar => new(Table.MaximumPosition, Table.VisibleRowCount, () => Table.FirstVisibleRow, Table.Scroll);

        /// <summary>
        /// Gets the number of visible lines.
        /// </summary>
        public int VisibleRowCount => Table.VisibleRowCount;

        // Gets the wrapped table control host.
        private TableControlHost Table => (TableControlHost)Element;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets a data row by its absolute index, independent of scrolling.
        /// </summary>
        /// <param name="row">The zero-based data row.</param>
        /// <returns>The row, whose selection can be read and changed.</returns>
        public GuiTableRow GetAbsoluteRow(int row)
        {
            // Read the selection once, so a row outside the data fails here rather than at first use.
            _ = Table.GetRowSelected(row);

            return new GuiTableRow(Table, row);
        }

        /// <summary>
        /// Gets the cell of a visible line.
        /// </summary>
        /// <param name="row">The zero-based visible line, not the data row.</param>
        /// <param name="column">The zero-based column.</param>
        /// <returns>The cell, such as a <c>GuiTextField</c>.</returns>
        public object GetCell(int row, int column)
        {
            return GuiComponentFactory.NewComponent(Table.GetCell(row, column));
        }
        #endregion
    }
}
