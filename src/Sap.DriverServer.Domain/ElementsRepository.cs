using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Http;

using Sap.DriverServer.Models;

namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Default <see cref="IElementsRepository"/> backed by the session dictionary and the scripting host.
    /// </summary>
    /// <param name="sessions">The shared dictionary of active sessions, registered as a singleton.</param>
    /// <param name="host">The scripting host that runs every SAP call on the STA thread.</param>
    /// <param name="settings">The driver settings that provide the busy wait.</param>
    public class ElementsRepository(
        IDictionary<string, SapSessionModel> sessions,
        ISapScriptingHost host,
        SapDriverSettings settings) : IElementsRepository
    {
        #region *** Fields       ***
        // Bounds how long each operation waits for a busy SAP session.
        private readonly int _busyWaitMilliseconds = settings.BusyWaitMilliseconds;

        // Runs every SAP call on the STA dispatcher thread.
        private readonly ISapScriptingHost _host = host;

        // Holds the active sessions and their element reference caches.
        private readonly IDictionary<string, SapSessionModel> _sessions = sessions;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        public int ClearValue(string session, string element)
        {
            // Clear the control's text; non-text controls raise "element not interactable".
            return InvokeOnElement(session, element, node =>
            {
                SapControlInteractions.ClearText(node);
                return true;
            }).StatusCode;
        }

        /// <inheritdoc />
        public (int Status, SapElementModel ElementModel) FindElement(string session, LocationStrategyModel locationStrategy)
        {
            // Route through the complete overload with the session as the search context.
            return FindElement(session, element: null, locationStrategy);
        }

        /// <inheritdoc />
        public (int Status, SapElementModel ElementModel) FindElement(string session, string element, LocationStrategyModel locationStrategy)
        {
            // Return the first match in document order, or 404 when nothing matches.
            var elementModel = FindElements(session, element, locationStrategy).FirstOrDefault();

            return elementModel is null
                ? (StatusCodes.Status404NotFound, default)
                : (StatusCodes.Status200OK, elementModel);
        }

        /// <inheritdoc />
        public (int StatusCode, SapElementAtModel ElementAt) FindElementAt(string session, SapPointModel point)
        {
            // Require the point; the controller binds it from the request body.
            ArgumentNullException.ThrowIfNull(argument: point);

            // Report an unknown session as a miss; the middleware reports the invalid session first.
            if (session is null || !_sessions.TryGetValue(session, out var sessionModel))
            {
                return (StatusCodes.Status404NotFound, default);
            }

            // Hit-test the attached session once it is idle, reading the control's identity and geometry.
            var busyWait = _busyWaitMilliseconds;
            var elementAt = _host.Invoke(application =>
            {
                var sessionNode = SapGuiNavigator.GetSession(application, sessionModel, busyWait);
                var (node, innerObject) = SapGuiNavigator.FindByPosition(application, sessionNode, point.X, point.Y);

                return node is null ? null : new SapElementAtModel
                {
                    InnerObject = innerObject,
                    Rect = ElementRectModel.ConvertFromRectangle(SapGuiNavigator.GetRectangle(node)),
                    SapId = node.Id,
                    Type = node.Type
                };
            });

            // Issue the reference outside the operation; the session cache is not a SAP object.
            if (elementAt is null)
            {
                return (StatusCodes.Status404NotFound, default);
            }

            var match = new SapElementModel { SapId = elementAt.SapId, Type = elementAt.Type };
            elementAt.Element = GetOrAddReference(sessionModel, match).Id;

            return (StatusCodes.Status200OK, elementAt);
        }

        /// <inheritdoc />
        public IEnumerable<SapElementModel> FindElements(string session, LocationStrategyModel locationStrategy)
        {
            // Route through the complete overload with the session as the search context.
            return FindElements(session, element: null, locationStrategy);
        }

        /// <inheritdoc />
        public IEnumerable<SapElementModel> FindElements(string session, string element, LocationStrategyModel locationStrategy)
        {
            // Return no matches for an unknown session; the middleware reports the invalid session first.
            if (session is null || !_sessions.TryGetValue(session, out var sessionModel))
            {
                return [];
            }

            // Resolve the parent reference before touching SAP GUI; an unknown reference is stale.
            var parent = element is null ? null : GetElement(session, element);
            if (element is not null && parent is null)
            {
                throw NewStaleElementException(session);
            }

            // Evaluate the locator in the parent's subtree, or in the whole session.
            var busyWait = _busyWaitMilliseconds;
            var matches = _host.Invoke(application =>
            {
                var sessionNode = SapGuiNavigator.GetSession(application, sessionModel, busyWait);
                var context = parent is null ? sessionNode : application.FindById(parent.SapId);
                if (context is null)
                {
                    throw NewStaleElementException(session);
                }

                return ElementLocator.FindElements(application, context, locationStrategy);
            });

            // Issue one stable reference per SAP identifier.
            return matches
                .Select(i => GetOrAddReference(sessionModel, i))
                .ToList();
        }

        /// <inheritdoc />
        public SapElementModel GetElement(string session, string element)
        {
            // Look up the reference without touching SAP GUI.
            if (session is null || element is null || !_sessions.TryGetValue(session, out var sessionModel))
            {
                return null;
            }

            return sessionModel.Elements.TryGetValue(element, out var elementModel) ? elementModel : null;
        }

        /// <inheritdoc />
        public (int StatusCode, string Value) GetElementAttribute(string session, string element, string name)
        {
            // Read the whitelisted attribute; unknown names yield an empty string.
            return InvokeOnElement(session, element, node => SapControlInteractions.GetAttribute(node, name));
        }

        /// <inheritdoc />
        public (int StatusCode, ElementRectModel Rect) GetElementRect(string session, string element)
        {
            // Read the control's screen geometry and convert it to the W3C shape.
            return InvokeOnElement(session, element, node => ElementRectModel.ConvertFromRectangle(SapGuiNavigator.GetRectangle(node)));
        }

        /// <inheritdoc />
        public (int StatusCode, string Text) GetElementText(string session, string element)
        {
            // Read the visible text; password fields yield an empty string.
            return InvokeOnElement(session, element, SapControlInteractions.GetText);
        }

        /// <inheritdoc />
        public int InvokeClick(string session, string element)
        {
            // Perform the control family's click equivalent.
            return InvokeOnElement(session, element, node =>
            {
                SapControlInteractions.InvokeClick(node);
                return true;
            }).StatusCode;
        }

        /// <inheritdoc />
        public (int StatusCode, object Value) InvokeElementMethod(string session, string element, SapMethodCallModel call)
        {
            // Call the allow-listed method; controls it returns become element references in this session's cache.
            return InvokeOnElement(
                session,
                element,
                node => SapMemberInteractions.InvokeMethod(node, call, result => NewElementReference(session, result)));
        }

        /// <inheritdoc />
        public int SetElementProperty(string session, string element, SapPropertyModel property)
        {
            // Set the allow-listed property; names outside the control family's list raise "unsupported operation".
            return InvokeOnElement(session, element, node =>
            {
                SapMemberInteractions.SetProperty(node, property);
                return true;
            }).StatusCode;
        }

        /// <inheritdoc />
        public int SetValue(string session, string element, string text)
        {
            // Replace the control's text; non-text controls raise "element not interactable".
            return InvokeOnElement(session, element, node =>
            {
                SapControlInteractions.SetText(node, text);
                return true;
            }).StatusCode;
        }

        // Returns the existing reference for a SAP identifier, or issues a new one in the session cache.
        // Concurrent lookups of the same identifier can at worst issue two references; both stay valid.
        private static SapElementModel GetOrAddReference(SapSessionModel sessionModel, SapElementModel match)
        {
            // Reuse the reference already issued for this control.
            var existing = sessionModel.Elements.Values.FirstOrDefault(i => string.Equals(i.SapId, match.SapId, StringComparison.Ordinal));
            if (existing is not null)
            {
                return existing;
            }

            // Issue a new reference; the type is kept for diagnostics only.
            var elementModel = new SapElementModel
            {
                Id = $"{Guid.NewGuid()}",
                SapId = match.SapId,
                Type = match.Type
            };
            sessionModel.Elements[elementModel.Id] = elementModel;

            return elementModel;
        }

        // Builds the stale element failure for a parent reference that is unknown or no longer resolves.
        private static SapDriverException NewStaleElementException(string session)
        {
            // Use the UIA wording so clients matching on the message keep working.
            var message = "Stale element reference: element is not attached to the application document " +
                $"(Session info: SapDriver={session})";

            return new SapDriverException(
                WebDriverErrorCodes.StaleElementReference,
                errorKind: null,
                StatusCodes.Status404NotFound,
                message);
        }

        // Resolves an element reference to its live control and runs an operation on it.
        // Returns 404 when the session or reference is unknown or the control no longer exists; the operation
        // runs on the STA thread after the session is idle.
        private (int StatusCode, T Result) InvokeOnElement<T>(string session, string element, Func<ISapGuiNode, T> operation)
        {
            // Validate the session and reference without touching SAP GUI.
            var elementModel = GetElement(session, element);
            if (elementModel is null || !_sessions.TryGetValue(session, out var sessionModel))
            {
                return (StatusCodes.Status404NotFound, default);
            }

            // Resolve the control and run the operation once the session is idle.
            var busyWait = _busyWaitMilliseconds;

            return _host.Invoke(application =>
            {
                SapGuiNavigator.GetSession(application, sessionModel, busyWait);

                var node = application.FindById(elementModel.SapId);
                return node is null
                    ? (StatusCodes.Status404NotFound, default(T))
                    : (StatusCodes.Status200OK, operation(node));
            });
        }

        // Issues an element reference for a control a method returned, in the W3C reference shape.
        private Dictionary<string, object> NewElementReference(string session, ISapGuiNode node)
        {
            // Reuse or add the session cache entry, so the client can use the reference with every element endpoint.
            var match = new SapElementModel { SapId = node.Id, Type = node.Type };
            var elementModel = GetOrAddReference(_sessions[session], match);

            return new Dictionary<string, object> { [ElementProperties.ElementReference] = elementModel.Id };
        }
        #endregion
    }
}
