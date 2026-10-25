using System;
using System.Threading;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Default <see cref="IActionsRepository"/> that sends <see cref="SapKeyMapper"/> steps through the scripting host.
    /// </summary>
    /// <param name="host">The scripting host that runs every SAP call on the STA thread.</param>
    /// <param name="settings">The driver settings that provide the busy wait.</param>
    public class ActionsRepository(ISapScriptingHost host, SapDriverSettings settings) : IActionsRepository
    {
        #region *** Fields       ***
        // Bounds how long each key waits for the session to finish the previous round trip.
        private readonly int _busyWaitMilliseconds = settings.BusyWaitMilliseconds;

        // Runs every SAP call on the STA dispatcher thread.
        private readonly ISapScriptingHost _host = host;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public void SendActions(SapSessionModel session, ActionsModel actionsModel)
        {
            // Require the session; the controller resolves it before calling.
            ArgumentNullException.ThrowIfNull(argument: session);

            // Validate and convert the whole payload first, so an unsupported action sends nothing.
            var steps = SapKeyMapper.ConvertToSteps(actionsModel);

            foreach (var step in steps)
            {
                // Pause on the request thread, leaving the STA thread free for other requests.
                if (step.VirtualKey == SapKeyMapper.PauseStep)
                {
                    Thread.Sleep(step.PauseMilliseconds);
                    continue;
                }

                // Send each key as its own operation, so the busy wait covers the round trip of the previous key.
                var busyWait = _busyWaitMilliseconds;
                _host.Invoke(application =>
                {
                    var sessionNode = SapGuiNavigator.GetSession(application, session, busyWait);
                    var window = SapGuiNavigator.GetWindow(sessionNode, session.WindowName);
                    window.InvokeMethod("sendVKey", step.VirtualKey);
                });
            }
        }
        #endregion
    }
}
