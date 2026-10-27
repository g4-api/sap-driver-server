using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Defines a screen that checks its entries when the user presses Enter without an OK code.
    /// </summary>
    /// <remarks>
    /// SAP runs the screen's field checks on every round trip. The simulator runs them on Enter only, which is enough
    /// to show the typical messages: a missing required entry and a value of the wrong format.
    /// </remarks>
    public interface IScreenCheck
    {
        #region *** Methods      ***
        /// <summary>
        /// Checks the screen's entries and moves the focus to the first field that failed.
        /// </summary>
        /// <returns>An error message for the first failed check; otherwise a success message.</returns>
        StatusMessage ConfirmEntries();
        #endregion
    }
}
