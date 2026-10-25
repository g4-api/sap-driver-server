using System;
using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiSessionInfo</c>: the system, client, and current screen of a session.
    /// </summary>
    /// <remarks>
    /// The logon user is not reported: the simulator does not keep it, so the object exposes no <c>User</c> member.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiSessionInfo : ApartmentBoundObject
    {
        #region *** Fields       ***
        // Provides the client and the current screen.
        private readonly SimulatedSession _session;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiSessionInfo"/> class.
        /// </summary>
        /// <param name="session">The simulated session.</param>
        public GuiSessionInfo(SimulatedSession session)
        {
            // Require the session; every property reads it on demand.
            ArgumentNullException.ThrowIfNull(argument: session);

            _session = session;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the application server name.
        /// </summary>
        public string ApplicationServer => "simulator";

        /// <summary>
        /// Gets the client, such as <c>001</c>.
        /// </summary>
        public string Client => _session.Client;

        /// <summary>
        /// Gets the logon language.
        /// </summary>
        public string Language => "EN";

        /// <summary>
        /// Gets the ABAP program of the current screen.
        /// </summary>
        public string Program => ScreenCatalog.GetScreen(_session.Screen).Program;

        /// <summary>
        /// Gets the number of the current screen.
        /// </summary>
        public int ScreenNumber => ScreenCatalog.GetScreen(_session.Screen).ScreenNumber;

        /// <summary>
        /// Gets the one-based session number, as shown in SAP GUI's status bar.
        /// </summary>
        public int SessionNumber => _session.Index + 1;

        /// <summary>
        /// Gets the system identifier.
        /// </summary>
        public string SystemName => SimulatedSystem.SystemName;

        /// <summary>
        /// Gets the current transaction.
        /// </summary>
        public string Transaction => _session.Screen;
        #endregion
    }
}
