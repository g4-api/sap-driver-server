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
    /// <para>
    /// Read-only fields, such as output-only dynpro fields, and disabled fields report <c>Changeable = false</c> and
    /// refuse text changes.
    /// </para>
    /// <para>
    /// <c>Text</c> is the field content without leading and trailing blanks, while <c>DisplayedText</c> keeps them, so
    /// padded output fields read differently through the two properties. Text longer than <c>MaxLength</c> is cut to
    /// the maximum length when a script sets it. Both follow the SAP GUI Scripting documentation and scripting
    /// experience; they have not been compared with a live SAP system.
    /// </para>
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
        /// Gets the text as displayed, including leading and trailing blanks.
        /// </summary>
        public string DisplayedText => TextBox.Text;

        /// <summary>
        /// Gets a value indicating whether the field is an output-only field.
        /// </summary>
        public bool IsOField => TextBox.IsReadOnly;

        /// <summary>
        /// Gets the maximum number of characters; zero when unlimited.
        /// </summary>
        public int MaxLength => TextBox.MaxLength;

        /// <summary>
        /// Gets a value indicating whether the field is a numeric field.
        /// </summary>
        public bool Numerical => DynproField.GetIsNumerical(Element);

        /// <summary>
        /// Gets a value indicating whether the field is a required entry field.
        /// </summary>
        public bool Required => DynproField.GetIsRequired(Element);

        // Gets the wrapped text box.
        private TextBox TextBox => (TextBox)Element;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override string GetText()
        {
            // Strip the blanks that padded fields show; DisplayedText keeps them.
            return TextBox.Text.Trim(' ');
        }

        /// <inheritdoc />
        protected override void SetText(string value)
        {
            // Refuse changes to output-only and disabled fields, as SAP GUI does.
            if (!Changeable)
            {
                throw new SimulatorException($"The field '{Id}' is not changeable.");
            }

            // Cut text that exceeds the field length; the text box limit applies only to typing.
            var text = value ?? string.Empty;
            TextBox.Text = MaxLength > 0 && text.Length > MaxLength ? text[..MaxLength] : text;
        }
        #endregion
    }
}
