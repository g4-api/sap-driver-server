using System.Windows.Controls;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the tab strip test lab (transaction <c>ZSIM_TABS</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The main tab strip <c>tabsTS_MAIN</c> fills its subscreen area <c>SUB_MAIN</c> with a different subscreen per
    /// tab, so the area's identifier segment changes with the tab, as in <c>ssubSUB_MAIN:SAPMZSIM_TABS:0110</c> and
    /// <c>ssubSUB_MAIN:SAPMZSIM_TABS:0120</c>. The note field exists on two tabs and therefore has two identifiers. The
    /// third tab nests the tab strip <c>tabsTS_INNER</c>.
    /// </para>
    /// <para>
    /// Only the selected tab's content is part of the scripting tree; an identifier recorded on another tab fails until
    /// that tab is selected. Entries are kept when tabs change and reset when the transaction starts again.
    /// </para>
    /// </remarks>
    public partial class TabsScreen : UserControl
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="TabsScreen"/> class.
        /// </summary>
        public TabsScreen()
        {
            InitializeComponent();
        }
        #endregion
    }
}
