using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiTableRow</c>: one data row of a table control, with its selection.
    /// </summary>
    /// <remarks>
    /// The object names a data row, not a visible line, so its selection stays with the row while the table scrolls.
    /// A row object for a line past the last row is not selectable and refuses selection changes.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiTableRow : ApartmentBoundObject
    {
        #region *** Fields       ***
        // Holds the zero-based data row this object names.
        private readonly int _row;

        // Holds the table that owns the row.
        private readonly TableControlHost _table;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTableRow"/> class.
        /// </summary>
        /// <param name="table">The table that owns the row.</param>
        /// <param name="row">The zero-based data row.</param>
        internal GuiTableRow(TableControlHost table, int row)
        {
            _table = table;
            _row = row;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the number of cells in the row: the number of columns.
        /// </summary>
        public int Count => _table.Columns.Count;

        /// <summary>
        /// Gets a value indicating whether the row exists and can be selected.
        /// </summary>
        public bool Selectable => _row < _table.RowCount;

        /// <summary>
        /// Gets or sets a value indicating whether the row is selected.
        /// </summary>
        public bool Selected
        {
            get => Selectable && _table.GetRowSelected(_row);
            set => _table.SetRowSelected(_row, value);
        }

        /// <summary>
        /// Gets the scripting type name.
        /// </summary>
        public string Type => nameof(GuiTableRow);
        #endregion
    }
}
