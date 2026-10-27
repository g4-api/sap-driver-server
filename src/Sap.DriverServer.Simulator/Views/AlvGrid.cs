using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows an ALV grid: a data grid with a toolbar, context menus, hotspot columns, and rows that load on demand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grid keeps what SAP's ALV grid control adds to a plain grid view. Toolbar buttons and menu items carry
    /// function codes, such as <c>&amp;SORT_ASC</c>; pressing one, by mouse or by script, hands the code to
    /// <see cref="FunctionHandler"/>. A hotspot cell click hands over <c>&amp;IC1</c>, as SAP's hotspot event does.
    /// </para>
    /// <para>
    /// The front end of an ALV grid holds only the rows that have been shown. The grid models that: a row counts as
    /// loaded once it was in view, and reading an unloaded row's cell through scripting returns an empty string until a
    /// script scrolls it into view with <c>firstVisibleRow</c>. Sorting or replacing the rows unloads them again. This
    /// follows scripting experience; it has not been compared with a live SAP system.
    /// </para>
    /// </remarks>
    public sealed class AlvGrid : DataGrid
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the function code of a hotspot click.
        /// </summary>
        public const string HotspotFunction = "&IC1";

        // Names the scroll viewer part of the data grid template.
        private const string ScrollViewerPart = "DG_ScrollViewer";
        #endregion

        #region *** Fields       ***
        // Records which rows have been in view; reset whenever the rows are replaced.
        private bool[] _loaded = [];

        // Holds the items of the open context menu; null when no menu is open.
        private IReadOnlyList<MenuEntry> _openMenu;

        // Holds the template's scroll viewer, whose offsets count rows because the grid scrolls by item.
        private ScrollViewer _scrollHost;
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the items of the cell context menu.
        /// </summary>
        public IReadOnlyList<MenuEntry> CellMenu { get; set; } = [];

        /// <summary>
        /// Gets or sets the handler that runs a function code from the toolbar, a menu, or a hotspot click.
        /// </summary>
        public Action<string> FunctionHandler { get; set; }

        /// <summary>
        /// Gets or sets the SAP names of the hotspot columns.
        /// </summary>
        public IReadOnlyCollection<string> HotspotColumns { get; set; } = [];

        /// <summary>
        /// Gets a value indicating whether a context menu is open and waits for a selection.
        /// </summary>
        public bool IsMenuOpen => _openMenu is not null;

        /// <summary>
        /// Gets or sets the toolbar buttons in display order.
        /// </summary>
        public IReadOnlyList<ToolbarEntry> ToolbarButtons { get; set; } = [];
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Clicks the current cell: runs <see cref="HotspotFunction"/> when the cell is in a hotspot column.
        /// </summary>
        /// <exception cref="SimulatorException">There is no current cell, or its column has no hotspot.</exception>
        public void ClickCurrentCell()
        {
            // Accept only hotspot cells; SAP ignores a click elsewhere, the simulator reports it to the script.
            var column = CurrentCell.Column?.SortMemberPath;
            if (column is null || !HotspotColumns.Contains(column))
            {
                throw new SimulatorException("The current cell is not a hotspot.");
            }

            FunctionHandler?.Invoke(HotspotFunction);
        }

        /// <inheritdoc />
        public override void OnApplyTemplate()
        {
            // Follow the scroll viewer, which reports every row that comes into view.
            base.OnApplyTemplate();
            if (_scrollHost is not null)
            {
                _scrollHost.ScrollChanged -= OnScrollHostScrollChanged;
            }

            _scrollHost = GetTemplateChild(ScrollViewerPart) as ScrollViewer;
            if (_scrollHost is not null)
            {
                _scrollHost.ScrollChanged += OnScrollHostScrollChanged;
            }
        }

        /// <summary>
        /// Opens the cell context menu for the current cell.
        /// </summary>
        public void OpenCellMenu()
        {
            OpenMenu(CellMenu);
        }

        /// <summary>
        /// Presses a toolbar button: runs a plain button's function, or opens a menu button's menu.
        /// </summary>
        /// <param name="buttonId">The button's function code, such as <c>&amp;REFRESH</c>.</param>
        /// <param name="isContextButton">True for <c>pressToolbarContextButton</c>, which opens the button's menu.</param>
        /// <exception cref="SimulatorException">The button does not exist, or the call does not match its type.</exception>
        public void PressToolbarButton(string buttonId, bool isContextButton)
        {
            // Find the button; menu buttons open their menu, plain buttons run their function.
            var button = ToolbarButtons.FirstOrDefault(i => string.Equals(i.Id, buttonId, StringComparison.Ordinal))
                ?? throw new SimulatorException($"The grid toolbar has no button '{buttonId}'.");
            var isMenuButton = button.Menu.Count > 0;
            if (isMenuButton != isContextButton)
            {
                var message = isMenuButton
                    ? $"The button '{buttonId}' opens a menu; press it with pressToolbarContextButton."
                    : $"The button '{buttonId}' has no menu; press it with pressToolbarButton.";
                throw new SimulatorException(message);
            }

            if (isMenuButton)
            {
                OpenMenu(button.Menu);
                return;
            }

            FunctionHandler?.Invoke(button.Id);
        }

        /// <summary>
        /// Selects an item of the open context menu and closes the menu.
        /// </summary>
        /// <param name="functionCode">The item's function code, such as <c>&amp;XXL</c>.</param>
        /// <exception cref="SimulatorException">No menu is open, or the menu has no such item.</exception>
        public void SelectMenuItem(string functionCode)
        {
            // Require an open menu and a matching item, then close the menu before the function runs.
            var item = GetOpenMenu().FirstOrDefault(i => string.Equals(i.Id, functionCode, StringComparison.Ordinal))
                ?? throw new SimulatorException($"The open context menu has no item '{functionCode}'.");
            CloseMenu();
            FunctionHandler?.Invoke(item.Id);
        }

        /// <summary>
        /// Selects an item of the open context menu by its text and closes the menu.
        /// </summary>
        /// <param name="text">The item text, such as <c>Spreadsheet...</c>.</param>
        /// <exception cref="SimulatorException">No menu is open, or the menu has no such item.</exception>
        public void SelectMenuItemByText(string text)
        {
            // Map the text to the item's function code, then select it by code.
            var item = GetOpenMenu().FirstOrDefault(i => string.Equals(i.Text, text, StringComparison.Ordinal))
                ?? throw new SimulatorException($"The open context menu has no item '{text}'.");
            SelectMenuItem(item.Id);
        }

        /// <summary>
        /// Tests whether a row has been in view, which SAP's front end needs before it knows the row's values.
        /// </summary>
        /// <param name="row">The zero-based row.</param>
        /// <returns>True when the row was loaded.</returns>
        public bool TestRowLoaded(int row)
        {
            // Count the rows in view now, then answer from the record.
            UpdateLayout();
            MarkVisibleRowsLoaded();

            return row >= 0 && row < _loaded.Length && _loaded[row];
        }

        /// <inheritdoc />
        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            // New or reordered rows are unknown to the front end until they are in view again.
            base.OnItemsSourceChanged(oldValue, newValue);
            _loaded = new bool[Items.Count];
        }

        /// <inheritdoc />
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            // Treat a click on a hotspot cell as the hotspot event; other clicks select as usual.
            base.OnPreviewMouseLeftButtonUp(e);
            var cell = FindCell(e.OriginalSource as DependencyObject);
            if (cell is not null && HotspotColumns.Contains(cell.Column.SortMemberPath))
            {
                CurrentCell = new DataGridCellInfo(cell);
                FunctionHandler?.Invoke(HotspotFunction);
            }
        }

        // Finds the data grid cell that contains an element, such as the text block a click landed on.
        private static DataGridCell FindCell(DependencyObject element)
        {
            // Walk up the visual tree until a cell or the top is reached; content elements such as runs end the walk.
            while (element is Visual and not DataGridCell)
            {
                element = VisualTreeHelper.GetParent(element);
            }

            return element as DataGridCell;
        }

        // Closes the open context menu, both the record and the visible popup.
        private void CloseMenu()
        {
            // Forget the menu first, so a second selection fails like it does in SAP.
            _openMenu = null;
            if (ContextMenu is not null)
            {
                ContextMenu.IsOpen = false;
                ContextMenu = null;
            }
        }

        // Gets the open context menu, raising an error when none is open.
        private IReadOnlyList<MenuEntry> GetOpenMenu()
        {
            return _openMenu ?? throw new SimulatorException("No context menu is open.");
        }

        // Records the rows in view as loaded; the scroll viewer's offsets count rows.
        private void MarkVisibleRowsLoaded()
        {
            // Mark from the first visible row through the last, partly visible rows included.
            if (_scrollHost is null || _loaded.Length == 0)
            {
                return;
            }

            var first = (int)_scrollHost.VerticalOffset;
            var last = Math.Min(_loaded.Length - 1, first + (int)Math.Ceiling(_scrollHost.ViewportHeight));
            for (var row = Math.Max(0, first); row <= last; row++)
            {
                _loaded[row] = true;
            }
        }

        // Records the newly visible rows after every scroll or layout change.
        private void OnScrollHostScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            MarkVisibleRowsLoaded();
        }

        // Opens a menu: records its items for scripting and shows them as a context menu at the grid.
        private void OpenMenu(IReadOnlyList<MenuEntry> items)
        {
            // Replace any open menu; every visible item runs its function when clicked.
            CloseMenu();
            _openMenu = items;
            var menu = new ContextMenu { PlacementTarget = this };

            foreach (var item in items)
            {
                var menuItem = new MenuItem { Header = item.Text };
                menuItem.Click += (_, _) => SelectMenuItem(item.Id);
                menu.Items.Add(menuItem);
            }

            // Keep the scripting record open until an item is selected, even when the visible popup closes on its own,
            // as WPF closes context menus of inactive windows.
            ContextMenu = menu;
            menu.IsOpen = true;
        }
        #endregion

        #region *** Nested Types ***
        /// <summary>
        /// Describes one menu item: its function code and its text.
        /// </summary>
        public sealed class MenuEntry
        {
            #region *** Properties   ***
            /// <summary>
            /// Gets the function code, such as <c>&amp;XXL</c>.
            /// </summary>
            public string Id { get; init; }

            /// <summary>
            /// Gets the item text.
            /// </summary>
            public string Text { get; init; }
            #endregion
        }

        /// <summary>
        /// Describes one toolbar button: its function code, text, and an optional menu.
        /// </summary>
        /// <remarks>
        /// A button with menu items is a menu button: SAP reports its type as <c>Menu</c>, and scripts press it with
        /// <c>pressToolbarContextButton</c> before they select an item.
        /// </remarks>
        public sealed class ToolbarEntry
        {
            #region *** Properties   ***
            /// <summary>
            /// Gets the function code, such as <c>&amp;REFRESH</c>.
            /// </summary>
            public string Id { get; init; }

            /// <summary>
            /// Gets the menu items; empty for a plain button.
            /// </summary>
            public IReadOnlyList<MenuEntry> Menu { get; init; } = [];

            /// <summary>
            /// Gets the button text.
            /// </summary>
            public string Text { get; init; }

            /// <summary>
            /// Gets the type SAP reports: <c>Button</c> or <c>Menu</c>.
            /// </summary>
            public string Type => Menu.Count > 0 ? "Menu" : "Button";
            #endregion
        }
        #endregion
    }
}
