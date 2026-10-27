using System.Runtime.InteropServices;
using System.Windows;

using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's splitter shell (<c>GuiShell</c> with <c>SubType</c> <c>Splitter</c>): a shell that contains shells.
    /// </summary>
    /// <remarks>
    /// The splitter is the one shell with children: its panes <c>shellcont[0]</c> and <c>shellcont[1]</c>, each holding
    /// a shell. Identifiers therefore continue after the splitter's own <c>shell</c> segment, as in
    /// <c>cntlSPLITTER/shellcont/shell/shellcont[1]/shell</c>. See <see cref="SplitterHost"/>.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiSplit : GuiVContainer
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiSplit"/> class.
        /// </summary>
        /// <param name="element">The splitter.</param>
        public GuiSplit(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the sash is vertical, with the panes side by side.
        /// </summary>
        public bool IsVertical => SplitterHost.IsVertical;

        /// <summary>
        /// Gets the shell subtype, <c>Splitter</c>.
        /// </summary>
        public string SubType => "Splitter";

        /// <inheritdoc />
        public override string Type => nameof(GuiShell);
        #endregion
    }
}
