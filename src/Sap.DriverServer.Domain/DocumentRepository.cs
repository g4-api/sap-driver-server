using System.Collections.Generic;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Default <see cref="IDocumentRepository"/> that renders <see cref="DocumentObjectModelFactory"/> documents.
    /// </summary>
    /// <param name="sessions">The shared dictionary of active sessions, registered as a singleton.</param>
    /// <param name="host">The scripting host that runs every SAP call on the STA thread.</param>
    /// <param name="settings">The driver settings that provide the busy wait.</param>
    public class DocumentRepository(
        IDictionary<string, SapSessionModel> sessions,
        ISapScriptingHost host,
        SapDriverSettings settings) : IDocumentRepository
    {
        #region *** Fields       ***
        // Bounds how long each operation waits for a busy SAP session.
        private readonly int _busyWaitMilliseconds = settings.BusyWaitMilliseconds;

        // Runs every SAP call on the STA dispatcher thread.
        private readonly ISapScriptingHost _host = host;

        // Holds the active sessions.
        private readonly IDictionary<string, SapSessionModel> _sessions = sessions;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public (int StatusCode, string Result) GetElementSource(string session, SapElementModel element)
        {
            // Report unknown sessions and elements without touching SAP GUI.
            if (session is null || element is null || !_sessions.TryGetValue(session, out var sessionModel))
            {
                return (StatusCodes.Status404NotFound, default);
            }

            // Render the element's subtree once the session is idle; a vanished control is a 404.
            var busyWait = _busyWaitMilliseconds;
            var source = _host.Invoke(application =>
            {
                SapGuiNavigator.GetSession(application, sessionModel, busyWait);
                var node = application.FindById(element.SapId);

                return node is null ? null : DocumentObjectModelFactory.NewDocument(node).ToString();
            });

            return source is null
                ? (StatusCodes.Status404NotFound, default)
                : (StatusCodes.Status200OK, source);
        }

        /// <inheritdoc />
        public (int StatusCode, string Result) GetPageSource(string session)
        {
            // Report unknown sessions without touching SAP GUI.
            if (session is null || !_sessions.TryGetValue(session, out var sessionModel))
            {
                return (StatusCodes.Status404NotFound, default);
            }

            // Render the whole session, which includes every open window and popup.
            var busyWait = _busyWaitMilliseconds;
            var source = _host.Invoke(application =>
            {
                var sessionNode = SapGuiNavigator.GetSession(application, sessionModel, busyWait);
                return DocumentObjectModelFactory.NewDocument(sessionNode).ToString();
            });

            return (StatusCodes.Status200OK, source);
        }
        #endregion
    }
}
