using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows a splitter shell (<c>GuiSplit</c>): two panes side by side, each holding one shell.
    /// </summary>
    /// <remarks>
    /// The panes are the containers <c>shellcont[0]</c> and <c>shellcont[1]</c>, so the shells inside get identifiers
    /// such as <c>shell/shellcont[1]/shell</c>. The splitter itself is a shell and a container at once.
    /// </remarks>
    public sealed class SplitterHost : Grid
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="SplitterHost"/> class.
        /// </summary>
        /// <param name="left">The shell of the left pane; it must already carry its scripting identifier.</param>
        /// <param name="right">The shell of the right pane; it must already carry its scripting identifier.</param>
        public SplitterHost(FrameworkElement left, FrameworkElement right)
        {
            // Assert the panes' content before the layout is built.
            AssertArguments(left, right);

            // Lay out two resizable panes with a splitter between them.
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddPane(left, column: 0, index: 0);
            var splitter = new GridSplitter
            {
                Background = Brushes.LightSteelBlue,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Width = 5
            };
            SetColumn(splitter, 1);
            Children.Add(splitter);
            AddPane(right, column: 2, index: 1);

            return;

            // Asserts both pane shells before the layout is built; nothing is mutated, and a missing shell fails the
            // construction with an ArgumentNullException.
            static void AssertArguments(FrameworkElement left, FrameworkElement right)
            {
                // Require a shell for each pane.
                ArgumentNullException.ThrowIfNull(argument: left);
                ArgumentNullException.ThrowIfNull(argument: right);
            }
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the panes are split by a vertical sash, side by side; always true here.
        /// </summary>
        public static bool IsVertical => true;
        #endregion

        #region *** Methods      ***
        // Wraps a shell in a pane container marked shellcont[index] and places it in a grid column.
        private void AddPane(FrameworkElement shell, int column, int index)
        {
            // Mark the pane like SAP's splitter containers, then place the shell inside it.
            var pane = new Border { Child = shell };
            ScriptingTree.SetId(pane, string.Create(CultureInfo.InvariantCulture, $"shellcont[{index}]"));
            SetColumn(pane, column);
            Children.Add(pane);
        }
        #endregion
    }
}
