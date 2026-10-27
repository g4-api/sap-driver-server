namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Defines a screen that assigns functions to virtual keys the main window does not handle itself, such as paging.
    /// </summary>
    /// <remarks>
    /// The session asks the current screen before it reports a key as not assigned, so <c>sendVKey</c>,
    /// <c>isVKeyAllowed</c>, and keyboard input see the screen's keys the same way.
    /// </remarks>
    public interface IVirtualKeyScreen
    {
        #region *** Methods      ***
        /// <summary>
        /// Runs the screen's function for a virtual key.
        /// </summary>
        /// <param name="key">The virtual key, one the screen reported as assigned.</param>
        void SendVirtualKey(int key);

        /// <summary>
        /// Tests whether the screen assigns a function to a virtual key.
        /// </summary>
        /// <param name="key">The virtual key, such as 82 for Page Down.</param>
        /// <returns>True when the screen handles the key.</returns>
        bool TestVirtualKey(int key);
        #endregion
    }
}
