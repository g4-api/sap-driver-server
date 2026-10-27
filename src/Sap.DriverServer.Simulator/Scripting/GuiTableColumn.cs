using System.Runtime.InteropServices;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiTableColumn</c>: the name and title of one table control column.
    /// </summary>
    /// <remarks>
    /// SAP's column object is also a collection of the column's visible cells; the simulator models only the name and
    /// the title, which scripts use to find a column index for <c>getCell</c>.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiTableColumn : ApartmentBoundObject
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTableColumn"/> class.
        /// </summary>
        /// <param name="name">The SAP field name, such as <c>ITAB-MATNR</c>.</param>
        /// <param name="title">The column title.</param>
        internal GuiTableColumn(string name, string title)
        {
            Name = name;
            Title = title;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the SAP field name, such as <c>ITAB-MATNR</c>.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the column title shown in the header.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Gets the scripting type name.
        /// </summary>
        public string Type => nameof(GuiTableColumn);
        #endregion
    }
}
