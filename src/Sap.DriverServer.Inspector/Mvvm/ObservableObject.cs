using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Sap.DriverServer.Inspector.Mvvm
{
    /// <summary>
    /// Base class for view models that notify WPF bindings when a property changes.
    /// </summary>
    /// <remarks>
    /// The inspector keeps its MVVM plumbing in-house instead of taking a toolkit dependency. Derived classes call
    /// <see cref="SetProperty{T}(ref T, T, string)"/> from property setters; notifications are raised on the calling
    /// thread, so view models update bound state on the UI thread only.
    /// </remarks>
    public abstract class ObservableObject : INotifyPropertyChanged
    {
        #region *** Fields       ***
        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Raises <see cref="PropertyChanged"/> for a property whose value was computed rather than stored.
        /// </summary>
        /// <param name="propertyName">The property name; the caller member name when omitted at the call site.</param>
        /// <remarks>The <c>null</c> default is required by <see cref="CallerMemberNameAttribute"/>, which supplies the value.</remarks>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Notify bindings; the default value exists only for the compiler-supplied caller member name.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Stores a new property value and raises <see cref="PropertyChanged"/> when it differs from the current value.
        /// </summary>
        /// <typeparam name="T">The property type.</typeparam>
        /// <param name="field">The backing field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="propertyName">The property name; the caller member name when omitted at the call site.</param>
        /// <returns>True when the value changed; otherwise false.</returns>
        /// <remarks>The <c>null</c> default is required by <see cref="CallerMemberNameAttribute"/>, which supplies the value.</remarks>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            // Skip unchanged values so bindings do not refresh needlessly.
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            // Store, then notify, so handlers observe the new value.
            field = value;
            OnPropertyChanged(propertyName);

            return true;
        }
        #endregion
    }
}
