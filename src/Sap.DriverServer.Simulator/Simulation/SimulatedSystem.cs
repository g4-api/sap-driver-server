using System;
using System.Collections.Generic;
using System.Linq;

using Sap.DriverServer.Simulator.Hosting;

namespace Sap.DriverServer.Simulator.Simulation
{
    /// <summary>
    /// Represents the simulated SAP Logon process: one connection to the simulated system and its open sessions.
    /// </summary>
    /// <remarks>
    /// The system is created and used on the WPF UI thread only; scripting calls reach it on that thread because the
    /// scripting objects are bound to its apartment. Sessions keep their index after other sessions close, as SAP GUI
    /// keeps <c>ses[n]</c> identifiers stable. When the last session closes, <see cref="AllSessionsClosed"/> fires and
    /// the application ends.
    /// </remarks>
    public sealed class SimulatedSystem
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the connection description, which marks every simulator session as a test double.
        /// </summary>
        public const string ConnectionDescription = "SIM - SAP GUI Simulator (test double)";

        /// <summary>
        /// Specifies the identifier of the single connection.
        /// </summary>
        public const string ConnectionId = "/app/con[0]";

        /// <summary>
        /// Specifies the system identifier reported by every session.
        /// </summary>
        public const string SystemName = "SIM";
        #endregion

        #region *** Fields       ***
        // Holds the open sessions in index order; closed sessions are removed.
        private readonly List<SimulatedSession> _sessions = [];

        /// <summary>
        /// Occurs when the last open session closes.
        /// </summary>
        public event EventHandler AllSessionsClosed;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="SimulatedSystem"/> class.
        /// </summary>
        /// <param name="options">The command-line options that select the scenario.</param>
        public SimulatedSystem(SimulatorOptions options)
        {
            // Require the options; every scenario switch is read from them.
            ArgumentNullException.ThrowIfNull(argument: options);

            Options = options;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the command-line options.
        /// </summary>
        public SimulatorOptions Options { get; }

        /// <summary>
        /// Gets the open sessions in index order.
        /// </summary>
        public IReadOnlyList<SimulatedSession> Sessions => _sessions;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Removes a session after its main window closed, ending the application after the last one.
        /// </summary>
        /// <param name="session">The closed session.</param>
        public void RemoveSession(SimulatedSession session)
        {
            // Forget the session; notify once the system has no sessions left.
            _sessions.Remove(session);
            if (_sessions.Count == 0)
            {
                AllSessionsClosed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Opens the configured number of sessions, each on its logon screen.
        /// </summary>
        public void Start()
        {
            // Open each session's main window; indexes start at zero like SAP's ses[n].
            foreach (var index in Enumerable.Range(0, Options.Sessions))
            {
                var session = new SimulatedSession(system: this, index);
                _sessions.Add(session);
                session.Open();
            }
        }
        #endregion
    }
}
