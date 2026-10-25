using System;
using System.Windows.Input;

namespace Sap.DriverServer.Inspector.Mvvm
{
    /// <summary>
    /// Command that delegates execution and availability to view model callbacks.
    /// </summary>
    /// <remarks>
    /// Availability is re-queried through <see cref="CommandManager.RequerySuggested"/>, so buttons enable and disable
    /// automatically after user input. The command parameter is passed through untyped.
    /// </remarks>
    public sealed class RelayCommand : ICommand
    {
        #region *** Fields       ***
        // Decides whether the command can run; null means "always".
        private readonly Func<object, bool> _canExecute;

        // Runs the command with the bound command parameter.
        private readonly Action<object> _execute;

        /// <inheritdoc />
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class that is always available.
        /// </summary>
        /// <param name="execute">The action to run.</param>
        public RelayCommand(Action<object> execute)
            : this(execute, canExecute: null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelayCommand"/> class with an availability check.
        /// </summary>
        /// <param name="execute">The action to run.</param>
        /// <param name="canExecute">The availability check, or null for "always available".</param>
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute)
        {
            // Require the action; a command without one has no meaning.
            ArgumentNullException.ThrowIfNull(argument: execute);

            _execute = execute;
            _canExecute = canExecute;
        }
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public bool CanExecute(object parameter)
        {
            // Treat a missing availability check as "always available".
            return _canExecute?.Invoke(parameter) ?? true;
        }

        /// <inheritdoc />
        public void Execute(object parameter)
        {
            // Run the delegated action with the bound parameter.
            _execute(parameter);
        }
        #endregion
    }
}
