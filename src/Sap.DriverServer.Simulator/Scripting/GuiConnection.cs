using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiConnection</c>: the connection to the simulated system (<c>con[0]</c>).
    /// </summary>
    /// <remarks>
    /// With <c>--disabled-by-server</c>, the connection reports <c>DisabledByServer</c> and exposes no sessions, as
    /// SAP GUI does for a system whose administrator disabled scripting.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiConnection : GuiComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiConnection"/> class.
        /// </summary>
        /// <param name="system">The simulated system.</param>
        public GuiConnection(SimulatedSystem system)
            : base(system)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the sessions.
        /// </summary>
        public GuiCollection Children => new(GetChildComponents());

        /// <inheritdoc />
        public override bool ContainerType => true;

        /// <summary>
        /// Gets the SAP Logon description, which marks the connection as a test double.
        /// </summary>
        public string Description => SimulatedSystem.ConnectionDescription;

        /// <summary>
        /// Gets a value indicating whether the server disabled scripting for this connection.
        /// </summary>
        public bool DisabledByServer => SimulatedSystem.Options.IsDisabledByServer;

        /// <inheritdoc />
        public override string Id => SimulatedSystem.ConnectionId;

        /// <inheritdoc />
        public override object Parent => new GuiApplication(SimulatedSystem);

        /// <summary>
        /// Gets the sessions, under the name SAP also provides for them.
        /// </summary>
        public GuiCollection Sessions => new(GetChildComponents());

        /// <inheritdoc />
        internal override string Segment => "con[0]";
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Resolves an absolute or relative identifier.
        /// </summary>
        /// <param name="id">The identifier, such as <c>ses[0]</c>.</param>
        /// <param name="raise">True to raise an error when nothing matches; false to return null.</param>
        /// <returns>The matching object; null when nothing matches and <paramref name="raise"/> is false.</returns>
        public object FindById(string id, [Optional, DefaultParameterValue(true)] bool raise)
        {
            return FindComponent(id, raise);
        }

        /// <inheritdoc />
        internal override IReadOnlyList<GuiComponent> GetChildComponents()
        {
            // Hide every session when the server disabled scripting.
            return DisabledByServer
                ? []
                : SimulatedSystem.Sessions.Select(i => (GuiComponent)new GuiSession(i)).ToList();
        }
        #endregion
    }
}
