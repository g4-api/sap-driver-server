using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// A multi-line text box for code, such as JSON, with a line-number gutter and a Format corner button.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Modeled on the G4 Hub text boxes: the gutter on the left shows one number per line and scrolls with the text,
    /// and the Format button in the top-right corner shows while the box is hovered or focused. Lines never wrap, so
    /// every number matches one line of text; long lines scroll sideways instead.
    /// </para>
    /// <para>
    /// The box only presents the text. Formatting runs through <see cref="FormatCommand"/>, so the view model that owns
    /// the text also owns how it is checked and indented. Without a command the button stays hidden. The G4 theme
    /// supplies the template, with the parts <c>PART_ContentHost</c> and <c>PART_LineNumbers</c>.
    /// </para>
    /// </remarks>
    public class CodeTextBox : TextBox
    {
        #region *** Constants    ***
        // Names the template part that scrolls the text.
        private const string ContentHostPart = "PART_ContentHost";

        // Names the template part that shows the line numbers.
        private const string LineNumbersPart = "PART_LineNumbers";
        #endregion

        #region *** Fields       ***
        /// <summary>
        /// Identifies the <see cref="FormatCommand"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty FormatCommandProperty = DependencyProperty.Register(
            name: nameof(FormatCommand),
            propertyType: typeof(ICommand),
            ownerType: typeof(CodeTextBox),
            typeMetadata: new PropertyMetadata(null));

        // Shows the line numbers; null until the template is applied or when the template has no gutter.
        private TextBlock _lineNumbers;

        // Moves the line numbers with the text's vertical scroll position.
        private readonly TranslateTransform _lineNumbersOffset = new();

        // Scrolls the text; the gutter follows its vertical offset.
        private ScrollViewer _scrollHost;

        // Holds the number of lines the gutter shows, so it is rebuilt only when the count changes.
        private int _shownLineCount;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeTextBox"/> class.
        /// </summary>
        public CodeTextBox()
        {
            // Code is edited as typed: several lines, tabs as text, and no wrapping, so numbers match lines.
            AcceptsReturn = true;
            AcceptsTab = true;
            TextWrapping = TextWrapping.NoWrap;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the command the Format button runs, or null to hide the button.
        /// </summary>
        public ICommand FormatCommand
        {
            get => (ICommand)GetValue(FormatCommandProperty);
            set => SetValue(FormatCommandProperty, value);
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public override void OnApplyTemplate()
        {
            // Release the previous template's scroll host before wiring the new one.
            base.OnApplyTemplate();
            if (_scrollHost is not null)
            {
                _scrollHost.ScrollChanged -= OnScrollChanged;
            }

            // Find the gutter and the scroll host; a template without them simply shows no numbers.
            _lineNumbers = GetTemplateChild(LineNumbersPart) as TextBlock;
            _scrollHost = GetTemplateChild(ContentHostPart) as ScrollViewer;
            if (_lineNumbers is null || _scrollHost is null)
            {
                return;
            }

            // Follow the text's scroll position and number the current text.
            _lineNumbers.RenderTransform = _lineNumbersOffset;
            _scrollHost.ScrollChanged += OnScrollChanged;
            _shownLineCount = 0;
            UpdateLineNumbers();
        }

        /// <inheritdoc />
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            // Keep the first number level with the first line when the padding changes.
            base.OnPropertyChanged(e);
            if (e.Property == PaddingProperty)
            {
                UpdateLineNumbersMargin();
            }
        }

        /// <inheritdoc />
        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            // Renumber after every edit, including text set from a binding.
            base.OnTextChanged(e);
            UpdateLineNumbers();
        }

        // Counts the lines of a text: one more than its line breaks, and one for empty text.
        private static int GetLineCount(string text)
        {
            // An empty box still shows line 1, like an editor.
            return string.IsNullOrEmpty(text) ? 1 : text.Count(i => i == '\n') + 1;
        }

        // Builds the gutter text: the numbers from 1 to the line count, one per line.
        private static string NewLineNumbersText(int lineCount)
        {
            // Join the numbers with line breaks, so the gutter lines up with the text lines.
            var text = new StringBuilder();
            for (var line = 1; line <= lineCount; line++)
            {
                text.Append(line).Append('\n');
            }

            return text.ToString().TrimEnd('\n');
        }

        // Moves the gutter with the text, so each number stays beside its line while scrolling.
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Only the vertical offset matters; the gutter does not scroll sideways.
            _lineNumbersOffset.Y = -e.VerticalOffset;
        }

        // Rebuilds the gutter numbers when the line count changed.
        private void UpdateLineNumbers()
        {
            // Nothing to number until the template supplies the gutter.
            if (_lineNumbers is null)
            {
                return;
            }

            // Rebuild only on a new line count, so typing within a line costs nothing.
            var lineCount = GetLineCount(Text);
            if (lineCount != _shownLineCount)
            {
                _shownLineCount = lineCount;
                _lineNumbers.Text = NewLineNumbersText(lineCount);
            }

            UpdateLineNumbersMargin();
        }

        // Places the first number at the same height as the first line of text.
        private void UpdateLineNumbersMargin()
        {
            // The text box puts its padding inside the scrolled content, so the gutter starts at the same top padding.
            if (_lineNumbers is not null)
            {
                _lineNumbers.Margin = new Thickness(0, Math.Max(0, Padding.Top), 0, 0);
            }
        }
        #endregion
    }
}
