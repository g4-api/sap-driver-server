using System.Windows;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Holds the dynpro field attributes that a screen sets on its input fields: required entry, numeric format, and
    /// input help.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The attributes are attached properties, so screens declare them in XAML next to the scripting identifier. The
    /// field types read them: a required field reports <c>Required = true</c>, and a field with an input help opens
    /// it when the window receives F4 (virtual key 4) while the field has the focus. A numeric field reports
    /// <c>Numerical = true</c>.
    /// </para>
    /// <para>
    /// The behavior follows the SAP GUI Scripting documentation and common scripting experience; it has not been
    /// compared with a live SAP system.
    /// </para>
    /// </remarks>
    public static class DynproField
    {
        #region *** Fields       ***
        /// <summary>
        /// Identifies the attached property that marks a numeric field.
        /// </summary>
        public static readonly DependencyProperty IsNumericalProperty = DependencyProperty.RegisterAttached(
            "IsNumerical",
            typeof(bool),
            typeof(DynproField),
            new PropertyMetadata(defaultValue: false));

        /// <summary>
        /// Identifies the attached property that marks a required entry field.
        /// </summary>
        public static readonly DependencyProperty IsRequiredProperty = DependencyProperty.RegisterAttached(
            "IsRequired",
            typeof(bool),
            typeof(DynproField),
            new PropertyMetadata(defaultValue: false));

        /// <summary>
        /// Identifies the attached property that names a field's input help, one of the <see cref="ValueHelpCatalog"/>
        /// constants.
        /// </summary>
        public static readonly DependencyProperty ValueHelpProperty = DependencyProperty.RegisterAttached(
            "ValueHelp",
            typeof(string),
            typeof(DynproField),
            new PropertyMetadata(defaultValue: null));
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets a value indicating whether a field is a numeric field.
        /// </summary>
        /// <param name="element">The field.</param>
        /// <returns>True when the screen marked the field as numeric.</returns>
        public static bool GetIsNumerical(DependencyObject element)
        {
            return (bool)element.GetValue(IsNumericalProperty);
        }

        /// <summary>
        /// Gets a value indicating whether a field is a required entry field.
        /// </summary>
        /// <param name="element">The field.</param>
        /// <returns>True when the screen marked the field as required.</returns>
        public static bool GetIsRequired(DependencyObject element)
        {
            return (bool)element.GetValue(IsRequiredProperty);
        }

        /// <summary>
        /// Gets the name of a field's input help.
        /// </summary>
        /// <param name="element">The field.</param>
        /// <returns>The input help name, such as <c>CARRID</c>; null when the field has no input help.</returns>
        public static string GetValueHelp(DependencyObject element)
        {
            return (string)element.GetValue(ValueHelpProperty);
        }

        /// <summary>
        /// Marks a field as a numeric field.
        /// </summary>
        /// <param name="element">The field.</param>
        /// <param name="value">True to mark the field as numeric.</param>
        public static void SetIsNumerical(DependencyObject element, bool value)
        {
            element.SetValue(IsNumericalProperty, value);
        }

        /// <summary>
        /// Marks a field as a required entry field.
        /// </summary>
        /// <param name="element">The field.</param>
        /// <param name="value">True to mark the field as required.</param>
        public static void SetIsRequired(DependencyObject element, bool value)
        {
            element.SetValue(IsRequiredProperty, value);
        }

        /// <summary>
        /// Sets the name of a field's input help.
        /// </summary>
        /// <param name="element">The field.</param>
        /// <param name="value">The input help name, one of the <see cref="ValueHelpCatalog"/> constants.</param>
        public static void SetValueHelp(DependencyObject element, string value)
        {
            element.SetValue(ValueHelpProperty, value);
        }
        #endregion
    }
}
