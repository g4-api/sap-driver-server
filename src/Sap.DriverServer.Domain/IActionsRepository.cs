using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Executes W3C "Perform Actions" payloads as SAP virtual keys.
    /// </summary>
    public interface IActionsRepository
    {
        #region *** Methods      ***
        /// <summary>
        /// Validates the whole payload, then sends its virtual keys and pauses in order to the session's current window.
        /// </summary>
        /// <param name="session">The session state.</param>
        /// <param name="actionsModel">The W3C actions payload.</param>
        /// <exception cref="SapDriverException">The payload contains an action with no SAP virtual key equivalent.</exception>
        void SendActions(SapSessionModel session, ActionsModel actionsModel);
        #endregion
    }
}
