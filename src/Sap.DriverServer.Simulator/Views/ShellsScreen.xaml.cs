using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Shows the shell controls test lab (transaction <c>ZSIM_SHELLS</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The screen holds the controls that are not dynpro fields and the containers that change identifiers:
    /// </para>
    /// <list type="bullet">
    /// <item><description>A toolbar control (<c>cntlTOOLBAR/shellcont/shell</c>) with a plain, a disabled, a check, a button-and-menu, and a menu button.</description></item>
    /// <item><description>A splitter (<c>cntlSPLITTER/shellcont/shell</c>) with a text edit in <c>shellcont[0]</c> and a picture in <c>shellcont[1]</c>.</description></item>
    /// <item><description>A box (<c>boxZSIM_SHELLS-ADDRESS</c>) whose fields are its siblings, not its children.</description></item>
    /// <item><description>A scroll container (<c>ssubSCROLL_AREA:SAPMZSIM_SHELLS:0200</c>) with fields below its visible area.</description></item>
    /// </list>
    /// <para>
    /// Every action reports its result in the status bar. The content resets whenever the transaction starts again.
    /// </para>
    /// </remarks>
    public partial class ShellsScreen : UserControl
    {
        #region *** Constants    ***
        // Sets the number of fields in the scroll container; most of them start out of view.
        private const int ScrollLineCount = 25;

        // Holds the initial text of the text edit: three lines separated by line feeds.
        private const string TextEditText = "First line of the note.\nSecond line of the note.\nThird line of the note.";
        #endregion

        #region *** Fields       ***
        // Reports toolbar functions and picture clicks.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="ShellsScreen"/> class.
        /// </summary>
        /// <param name="session">The session that reports the actions.</param>
        public ShellsScreen(SimulatedSession session)
        {
            // Require the session that reports the actions, then build the screen.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
            InitializeComponent();

            // Fill the toolbar with one button of every type, one of them disabled.
            ToolbarShell.FunctionHandler = ReportFunction;
            ToolbarShell.SetButtons(
            [
                new() { Id = "SAVE", Text = "Save", Tooltip = "Save the note" },
                new() { Id = "DELETE", IsEnabled = false, Text = "Delete", Tooltip = "Delete (disabled)" },
                new() { Id = "EDIT_MODE", Text = "Edit Mode", Tooltip = "Toggle edit mode", Type = ShellToolbar.CheckBoxType },
                new()
                {
                    Id = "EXPORT",
                    Menu = [new() { Id = "EXPORT_XLS", Text = "As Spreadsheet" }, new() { Id = "EXPORT_PDF", Text = "As PDF" }],
                    Text = "Export",
                    Tooltip = "Export, or choose a format from the menu",
                    Type = ShellToolbar.ButtonAndMenuType
                },
                new()
                {
                    Id = "MORE",
                    Menu = [new() { Id = "MORE_A", Text = "Option A" }, new() { Id = "MORE_B", Text = "Option B" }],
                    Text = "More",
                    Tooltip = "More options",
                    Type = ShellToolbar.MenuType
                }
            ]);

            // Split the middle area into a text edit and a picture; each pane gets its shell identifier.
            var textEdit = new TextBox
            {
                AcceptsReturn = true,
                Text = TextEditText,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            ScriptingTree.SetId(textEdit, "shell");
            var picture = new PictureHost { ClickHandler = ReportPictureClick };
            ScriptingTree.SetId(picture, "shell");
            var splitter = new SplitterHost(textEdit, picture);
            ScriptingTree.SetId(splitter, "shell");
            SplitterArea.Child = splitter;

            // Fill the scroll container with numbered fields; only the first few fit its height.
            for (var line = 1; line <= ScrollLineCount; line++)
            {
                ScrollLines.Children.Add(NewScrollLine(line));
            }
        }
        #endregion

        #region *** Methods      ***
        // Creates one numbered label and field pair for the scroll container.
        private static StackPanel NewScrollLine(int line)
        {
            // Name both elements by line number, padded to two digits like dynpro field names.
            var suffix = line.ToString("00", CultureInfo.InvariantCulture);
            var label = new TextBlock { Text = $"Line {suffix}", VerticalAlignment = VerticalAlignment.Center, Width = 70 };
            ScriptingTree.SetId(label, $"lblZSIM_SHELLS-LINE{suffix}");
            var field = new TextBox { Text = $"Value {suffix}", Width = 160 };
            ScriptingTree.SetId(field, $"txtZSIM_SHELLS-LINE{suffix}");

            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 4), Orientation = Orientation.Horizontal };
            row.Children.Add(label);
            row.Children.Add(field);

            return row;
        }

        // Reports a toolbar function in the status bar.
        private void ReportFunction(string functionCode)
        {
            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "090", $"Toolbar function {functionCode} (simulated)."));
        }

        // Reports a picture click in the status bar.
        private void ReportPictureClick(string click)
        {
            _session.ShowStatus(StatusMessage.NewMessage(StatusMessage.Success, "091", $"Picture event {click}."));
        }
        #endregion
    }
}
