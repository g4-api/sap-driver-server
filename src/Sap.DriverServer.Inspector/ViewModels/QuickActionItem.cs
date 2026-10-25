using System.Windows.Input;

namespace Sap.DriverServer.Inspector.ViewModels
{
    /// <summary>
    /// Represents one quick-action button in the Actions section.
    /// </summary>
    /// <param name="label">The button label, such as <c>Press</c>.</param>
    /// <param name="command">The command that performs the action on the selected control.</param>
    public sealed class QuickActionItem(string label, ICommand command)
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the command that performs the action.
        /// </summary>
        public ICommand Command { get; } = command;

        /// <summary>
        /// Gets the button label.
        /// </summary>
        public string Label { get; } = label;
        #endregion
    }
}
