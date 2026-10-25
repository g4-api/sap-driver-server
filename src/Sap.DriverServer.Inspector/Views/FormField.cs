using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Shows one input with its label above it and a short hint between the label and the input.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The label is the <see cref="HeaderedContentControl.Header"/> and the input is the content. The hint explains what
    /// the input expects in one line, so the inspector stays usable without hovering for tool tips. The G4 theme
    /// supplies the template and hides the hint when it is empty.
    /// </para>
    /// <para>
    /// The field also labels its input for UI Automation: the input's name becomes the label and its help text becomes
    /// the hint, so screen readers announce the field as a labelled input. When the content is a panel, such as an
    /// input with a button beside it, the first text box or combo box in the panel is labelled. Names set in XAML win.
    /// </para>
    /// </remarks>
    public class FormField : HeaderedContentControl
    {
        #region *** Fields       ***
        /// <summary>
        /// Identifies the <see cref="Hint"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty HintProperty = DependencyProperty.Register(
            name: nameof(Hint),
            propertyType: typeof(string),
            ownerType: typeof(FormField),
            typeMetadata: new PropertyMetadata(string.Empty, OnHintChanged));
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the one-line explanation shown under the label.
        /// </summary>
        public string Hint
        {
            get => (string)GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override void OnContentChanged(object oldContent, object newContent)
        {
            // Label the new input; the label and hint are usually set before the content in XAML.
            base.OnContentChanged(oldContent, newContent);
            SetAutomationLabels();
        }

        /// <inheritdoc />
        protected override void OnHeaderChanged(object oldHeader, object newHeader)
        {
            // Rename the input after the new label.
            base.OnHeaderChanged(oldHeader, newHeader);
            SetAutomationLabels();
        }

        // Finds the input to label: the content itself, or the first text box or combo box inside a content panel.
        private static Control FindInput(object content)
        {
            // A text box or combo box content is the input itself.
            if (content is TextBox or ComboBox)
            {
                return (Control)content;
            }

            // Search a panel depth-first, in the order its children were declared.
            return content is DependencyObject parent
                ? LogicalTreeHelper
                    .GetChildren(parent)
                    .OfType<object>()
                    .Select(FindInput)
                    .FirstOrDefault(i => i is not null)
                : null;
        }

        // Re-labels the input when the hint changes.
        private static void OnHintChanged(DependencyObject field, DependencyPropertyChangedEventArgs e)
        {
            // Describe the input with the new hint.
            ((FormField)field).SetAutomationLabels();
        }

        // Gives the input the label as its UI Automation name and the hint as its help text, unless XAML set them.
        private void SetAutomationLabels()
        {
            // Nothing to label until the content holds an input.
            var input = FindInput(Content);
            if (input is null)
            {
                return;
            }

            // Fill only empty values, so an explicit AutomationProperties setting in XAML is kept.
            if (string.IsNullOrEmpty(AutomationProperties.GetName(input)) && Header is string label)
            {
                AutomationProperties.SetName(input, label);
            }

            if (string.IsNullOrEmpty(AutomationProperties.GetHelpText(input)))
            {
                AutomationProperties.SetHelpText(input, Hint);
            }
        }
        #endregion
    }
}
