using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Sap.DriverServer.Inspector.Views
{
    /// <summary>
    /// Converts true to <see cref="Visibility.Collapsed"/> and everything else to <see cref="Visibility.Visible"/>.
    /// </summary>
    /// <remarks>
    /// The window uses it for notes that replace a section a source does not support, such as the member invoker in
    /// driver sessions. It is the inverse of the framework's <see cref="BooleanToVisibilityConverter"/>.
    /// </remarks>
    public sealed class CollapsedWhenTrueConverter : IValueConverter
    {
        #region *** Methods      ***
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Hide the element when the bound flag is set.
            return value is true ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // The binding is one-way; there is no flag to recover from a visibility.
            throw new NotSupportedException("CollapsedWhenTrueConverter supports one-way bindings only.");
        }
        #endregion
    }
}
