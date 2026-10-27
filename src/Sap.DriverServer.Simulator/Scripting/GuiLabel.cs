using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiLabel</c>: a label (<c>lbl</c>), whose text is read-only.
    /// </summary>
    /// <remarks>
    /// Labels of a list screen are named by character position, as in <c>lbl[10,5]</c>: column 10 of line 5 of the
    /// visible page. They report <c>IsListElement = true</c> and their position as <c>CharLeft</c> and <c>CharTop</c>.
    /// After the list scrolls, the same identifier names another list line.
    /// </remarks>
    [ComVisible(true)]
    public sealed partial class GuiLabel : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiLabel"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        public GuiLabel(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the character column of a list label; 0 for screen labels.
        /// </summary>
        public int CharLeft => GetListPosition(group: "left");

        /// <summary>
        /// Gets the character line of a list label on the visible page; 0 for screen labels.
        /// </summary>
        public int CharTop => GetListPosition(group: "top");

        /// <summary>
        /// Gets a value indicating whether the label is part of a list, named by its character position.
        /// </summary>
        public bool IsListElement => ListPositionPattern().IsMatch(Segment);
        #endregion

        #region *** Methods      ***
        // Matches a list label segment, such as lbl[10,5].
        [GeneratedRegex(@"^lbl\[(?<left>\d+),(?<top>\d+)\]$")]
        private static partial Regex ListPositionPattern();

        // Reads one coordinate of a list label's segment; screen labels have none.
        private int GetListPosition(string group)
        {
            // Parse the coordinate from the segment; a segment without a position reports 0.
            var match = ListPositionPattern().Match(Segment);

            return match.Success ? int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) : 0;
        }
        #endregion
    }
}
