using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's text edit shell (<c>GuiShell</c> with <c>SubType</c> <c>TextEdit</c>): a multi-line text control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unlike a text field, the text edit is a shell: its text is read and written through the shell's <c>text</c>
    /// property, lines are read with <c>getLineText</c>, and the selection is set with <c>setSelectionIndexes</c>.
    /// Lines are separated by line feeds and counted from 0 in the simulator.
    /// </para>
    /// <para>
    /// The members follow the SAP GUI Scripting documentation; their exact formats have not been compared with a live
    /// SAP system.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiTextedit : GuiShell
    {
        #region *** Constants    ***
        // Separates the lines of the text.
        private const char LineSeparator = '\n';
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTextedit"/> class.
        /// </summary>
        /// <param name="element">The multi-line text box.</param>
        public GuiTextedit(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <inheritdoc />
        public override bool Changeable => Editor.IsEnabled && !Editor.IsReadOnly;

        /// <summary>
        /// Gets the number of lines.
        /// </summary>
        public int LineCount => GetLines().Length;

        /// <summary>
        /// Gets the zero-based character index where the selection ends.
        /// </summary>
        public int SelectionIndexEnd => Editor.SelectionStart + Editor.SelectionLength;

        /// <summary>
        /// Gets the zero-based character index where the selection starts.
        /// </summary>
        public int SelectionIndexStart => Editor.SelectionStart;

        /// <inheritdoc />
        public override string SubType => "TextEdit";

        // Gets the wrapped text box.
        private TextBox Editor => (TextBox)Element;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the text of one line.
        /// </summary>
        /// <param name="line">The zero-based line.</param>
        /// <returns>The line text, without the line separator.</returns>
        /// <exception cref="SimulatorException">The line does not exist.</exception>
        public string GetLineText(int line)
        {
            // Reject lines outside the text with a message that names the line.
            var lines = GetLines();
            if (line < 0 || line >= lines.Length)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"The text edit '{Id}' has no line {line}.");
                throw new SimulatorException(message);
            }

            return lines[line];
        }

        /// <summary>
        /// Selects a range of characters.
        /// </summary>
        /// <param name="start">The zero-based index of the first selected character.</param>
        /// <param name="end">The zero-based index after the last selected character.</param>
        /// <exception cref="SimulatorException">The range is outside the text.</exception>
        public void SetSelectionIndexes(int start, int end)
        {
            // Reject ranges outside the text or backwards, then select.
            if (start < 0 || end < start || end > Editor.Text.Length)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"The selection {start} to {end} is outside the text of '{Id}'.");
                throw new SimulatorException(message);
            }

            Editor.Select(start, end - start);
        }

        /// <inheritdoc />
        protected override string GetText()
        {
            return Editor.Text;
        }

        /// <inheritdoc />
        protected override void SetText(string value)
        {
            // Refuse changes to read-only text, as SAP GUI does.
            if (!Changeable)
            {
                throw new SimulatorException($"The text edit '{Id}' is not changeable.");
            }

            Editor.Text = value ?? string.Empty;
        }

        // Splits the text into lines; an empty text has one empty line.
        private string[] GetLines()
        {
            return Editor.Text.Replace("\r", string.Empty, StringComparison.Ordinal).Split(LineSeparator);
        }
        #endregion
    }
}
