using System;
using System.Windows;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Gives a drop-down list grey placeholder text, so an empty box says why it is empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The G4 theme reads both properties in its combo box template. The empty text shows while the list has no items,
    /// and the list does not open then. The prompt shows while the list has items but none is selected. A selection
    /// replaces either text.
    /// </para>
    /// <para>
    /// The template also reports the shown text as the combo box's UI Automation item status, so screen readers announce
    /// the same state the placeholder shows. A combo box without these properties looks and behaves as before.
    /// </para>
    /// </remarks>
    public static class Placeholder
    {
        #region *** Fields       ***
        /// <summary>
        /// Identifies the Empty attached property.
        /// </summary>
        public static readonly DependencyProperty EmptyProperty = DependencyProperty.RegisterAttached(
            name: "Empty",
            propertyType: typeof(string),
            ownerType: typeof(Placeholder),
            defaultMetadata: new PropertyMetadata(string.Empty));

        /// <summary>
        /// Identifies the Prompt attached property.
        /// </summary>
        public static readonly DependencyProperty PromptProperty = DependencyProperty.RegisterAttached(
            name: "Prompt",
            propertyType: typeof(string),
            ownerType: typeof(Placeholder),
            defaultMetadata: new PropertyMetadata(string.Empty));
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the text shown while the list has no items.
        /// </summary>
        /// <param name="element">The combo box that shows the text.</param>
        /// <returns>The empty-list text, or an empty string when none is set.</returns>
        public static string GetEmpty(DependencyObject element)
        {
            // Fail fast on a missing element; WPF passes the target of the attached property.
            ArgumentNullException.ThrowIfNull(argument: element);
            return (string)element.GetValue(EmptyProperty);
        }

        /// <summary>
        /// Gets the text shown while the list has items but none is selected.
        /// </summary>
        /// <param name="element">The combo box that shows the text.</param>
        /// <returns>The prompt text, or an empty string when none is set.</returns>
        public static string GetPrompt(DependencyObject element)
        {
            // Fail fast on a missing element; WPF passes the target of the attached property.
            ArgumentNullException.ThrowIfNull(argument: element);
            return (string)element.GetValue(PromptProperty);
        }

        /// <summary>
        /// Sets the text shown while the list has no items.
        /// </summary>
        /// <param name="element">The combo box that shows the text.</param>
        /// <param name="value">The empty-list text, such as "No saved sets yet".</param>
        public static void SetEmpty(DependencyObject element, string value)
        {
            // Fail fast on a missing element, then store the text for the template to read.
            ArgumentNullException.ThrowIfNull(argument: element);
            element.SetValue(EmptyProperty, value);
        }

        /// <summary>
        /// Sets the text shown while the list has items but none is selected.
        /// </summary>
        /// <param name="element">The combo box that shows the text.</param>
        /// <param name="value">The prompt text, such as "Select a saved set".</param>
        public static void SetPrompt(DependencyObject element, string value)
        {
            // Fail fast on a missing element, then store the text for the template to read.
            ArgumentNullException.ThrowIfNull(argument: element);
            element.SetValue(PromptProperty, value);
        }
        #endregion
    }
}
