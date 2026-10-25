using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiTextField</c>: a single-line input field (<c>txt</c>).
    /// </summary>
    /// <remarks>
    /// Read-only fields, such as output-only dynpro fields, report <c>Changeable = false</c> and refuse text changes.
    /// </remarks>
    [ComVisible(true)]
    public class GuiTextField : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiTextField"/> class.
        /// </summary>
        /// <param name="element">The text box.</param>
        public GuiTextField(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the zero-based caret position.
        /// </summary>
        public int CaretPosition
        {
            get => TextBox.CaretIndex;
            set => TextBox.CaretIndex = value;
        }

        /// <inheritdoc />
        public override bool Changeable => TextBox.IsEnabled && !TextBox.IsReadOnly;

        /// <summary>
        /// Gets the text as displayed.
        /// </summary>
        public string DisplayedText => TextBox.Text;

        /// <summary>
        /// Gets the maximum number of characters; zero when unlimited.
        /// </summary>
        public int MaxLength => TextBox.MaxLength;

        // Gets the wrapped text box.
        private TextBox TextBox => (TextBox)Element;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override void SetText(string value)
        {
            // Refuse changes to output-only fields, as SAP GUI does.
            if (!Changeable)
            {
                throw new SimulatorException($"The field '{Id}' is not changeable.");
            }

            TextBox.Text = value ?? string.Empty;
        }
        #endregion
    }
}
