using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiSession</c>: one session (<c>ses[n]</c>) with its windows, session information, hit
    /// testing, and virtual key descriptions.
    /// </summary>
    /// <remarks>
    /// <c>findByPosition</c> returns a collection whose first item is the absolute identifier of the deepest control
    /// under the point and whose second item describes an inner object, such as a grid cell, or is empty. Windows are
    /// tested top-most first, so the popup wins over the main window. This is the simulator's model of SAP's result;
    /// it has not been compared with a real SAP GUI.
    /// <para>
    /// The session raises <c>Change</c> through one COM connection point while <c>Record</c> is true; see
    /// <see cref="SessionRecorder"/> for what is reported.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiSession : GuiComponent, IConnectionPointContainer
    {
        #region *** Constants    ***
        // Lists the modifier prefixes of the four function-key groups: 1-12, 13-24, 25-36, and 37-48.
        private static readonly string[] FunctionKeyPrefixes = [string.Empty, "Shift+", "Ctrl+", "Ctrl+Shift+"];

        // Lists the paging keys 80 to 83, in key order.
        private static readonly string[] PagingKeys = ["Ctrl+Page Up", "Page Up", "Page Down", "Ctrl+Page Down"];
        #endregion

        #region *** Fields       ***
        // Provides the windows, screen, and status of the session.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiSession"/> class.
        /// </summary>
        /// <param name="session">The simulated session.</param>
        public GuiSession(SimulatedSession session)
            : base(GetSystem(session))
        {
            // Keep the session; every member reads its windows and screen on demand.
            _session = session;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the active window: the popup while it is open, otherwise the main window.
        /// </summary>
        public object ActiveWindow => GuiComponentFactory.NewWindow(_session.ActiveWindow);

        /// <summary>
        /// Gets a value indicating whether a server round trip is running; the simulator answers at once, so always false.
        /// </summary>
        public bool Busy => _session.IsBusy;

        /// <summary>
        /// Gets the windows, main window first.
        /// </summary>
        public GuiCollection Children => new(GetChildComponents());

        /// <inheritdoc />
        public override bool ContainerType => true;

        /// <inheritdoc />
        public override string Id => _session.Id;

        /// <summary>
        /// Gets the session information: system, client, transaction, program, and screen.
        /// </summary>
        public GuiSessionInfo Info => new(_session);

        /// <inheritdoc />
        public override object Parent => new GuiConnection(SimulatedSystem);

        /// <summary>
        /// Gets or sets a value indicating whether the session records, which makes it raise <c>Change</c> events.
        /// </summary>
        /// <exception cref="SimulatorException">Recording is switched on while the scenario disables it.</exception>
        public bool Record
        {
            get => _session.Recorder.IsRecording;
            set => _session.Recorder.SetRecording(value);
        }

        /// <inheritdoc />
        internal override string Segment => string.Create(CultureInfo.InvariantCulture, $"ses[{_session.Index}]");
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        void IConnectionPointContainer.EnumConnectionPoints(out IEnumConnectionPoints ppEnum)
        {
            ppEnum = new GuiSessionConnectionPoint.Enumerator(new GuiSessionConnectionPoint(_session, container: this));
        }

        /// <inheritdoc />
        void IConnectionPointContainer.FindConnectionPoint(ref Guid riid, out IConnectionPoint ppCP)
        {
            // Offer only the session event interface; other identifiers report CONNECT_E_NOCONNECTION.
            if (riid != GuiSessionConnectionPoint.EventInterfaceId)
            {
                throw new COMException("The session has no connection point for this interface.", unchecked((int)0x80040200));
            }

            ppCP = new GuiSessionConnectionPoint(_session, container: this);
        }

        /// <summary>
        /// Resolves an absolute identifier, or one relative to the session, such as <c>wnd[0]/sbar</c>.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="raise">True to raise an error when nothing matches; false to return null.</param>
        /// <returns>The matching object; null when nothing matches and <paramref name="raise"/> is false.</returns>
        public object FindById(string id, [Optional, DefaultParameterValue(true)] bool raise)
        {
            return FindComponent(id, raise);
        }

        /// <summary>
        /// Finds the control under a screen point.
        /// </summary>
        /// <param name="x">The horizontal screen coordinate in physical pixels.</param>
        /// <param name="y">The vertical screen coordinate in physical pixels.</param>
        /// <param name="raise">True to raise an error when no window is under the point; false to return an empty collection.</param>
        /// <returns>The control's absolute identifier and the inner-object description, or an empty collection.</returns>
        /// <exception cref="SimulatorException">No window is under the point and <paramref name="raise"/> is true.</exception>
        public GuiCollection FindByPosition(int x, int y, [Optional, DefaultParameterValue(true)] bool raise)
        {
            // Test the windows top-most first and return the deepest control that contains the point.
            var point = new Point(x, y);

            foreach (var frame in _session.Windows.Reverse())
            {
                var window = GuiComponentFactory.NewWindow(frame);
                if (!window.TestContainsPoint(point))
                {
                    continue;
                }

                var hit = FindDeepest(window, point).Component;

                return new GuiCollection([hit.Id, hit.GetInnerObject(point)]);
            }

            // Report a point outside every window as SAP GUI does: an error, or nothing.
            if (raise)
            {
                var message = string.Create(CultureInfo.InvariantCulture, $"No control was found at the screen position ({x}, {y}).");
                throw new SimulatorException(message);
            }

            return new GuiCollection([]);
        }

        /// <summary>
        /// Gets the description of a virtual key, such as <c>Enter</c>, <c>F3</c>, or <c>Shift+F3</c>.
        /// </summary>
        /// <param name="key">The virtual key.</param>
        /// <returns>The description.</returns>
        /// <exception cref="SimulatorException">The key is not a virtual key the simulator knows.</exception>
        public string GetVKeyDescription(int key)
        {
            // Describe Enter, the four function-key groups, and the paging keys.
            return key switch
            {
                0 => "Enter",
                >= 1 and <= 48 => string.Create(CultureInfo.InvariantCulture, $"{FunctionKeyPrefixes[(key - 1) / 12]}F{((key - 1) % 12) + 1}"),
                >= 80 and <= 83 => PagingKeys[key - 80],
                _ => throw new SimulatorException(string.Create(CultureInfo.InvariantCulture, $"Virtual key {key} is not defined in {_session.Id}."))
            };
        }

        /// <summary>
        /// Runs an OK code, such as <c>/nFORM</c>, as if it was typed into the command field and confirmed.
        /// </summary>
        /// <param name="command">The OK code.</param>
        /// <exception cref="SimulatorException">The popup is open and blocks the main window.</exception>
        public void SendCommand(string command)
        {
            // Refuse commands while the popup blocks the main window, as for virtual keys.
            if (_session.Windows.Count > 1)
            {
                throw new SimulatorException("The window wnd[0] is blocked while the popup wnd[1] is open.");
            }

            _session.SubmitCommand(command);
        }

        /// <inheritdoc />
        internal override IReadOnlyList<GuiComponent> GetChildComponents()
        {
            return _session.Windows.Select(i => (GuiComponent)GuiComponentFactory.NewWindow(i)).ToList();
        }

        // Finds the deepest visual descendant that contains the point; the last one wins among equally deep matches.
        // The whole subtree is searched, because a tab's content lies outside the tab header's rectangle.
        private static (GuiVComponent Component, int Depth) FindDeepest(GuiVComponent component, Point point)
        {
            // Start from the component itself and keep the deepest match among its descendants.
            var best = (Component: component, Depth: 0);

            foreach (var child in component.GetChildComponents().OfType<GuiVComponent>())
            {
                var (match, depth) = FindDeepest(child, point);
                var isCandidate = match.TestContainsPoint(point) && depth + 1 >= best.Depth;
                if (isCandidate)
                {
                    best = (match, depth + 1);
                }
            }

            return best;
        }

        // Requires the session before the base constructor runs, and returns the system it belongs to.
        private static SimulatedSystem GetSystem(SimulatedSession session)
        {
            // Report a missing session under its own name, not as a missing system.
            ArgumentNullException.ThrowIfNull(argument: session);

            return session.System;
        }
        #endregion
    }
}
