using System.Collections.Generic;
using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiApplication</c>, the scripting engine: the root of the object tree (<c>/app</c>).
    /// </summary>
    /// <remarks>
    /// The simulator has exactly one connection, <c>con[0]</c>, to the simulated system.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiApplication : GuiComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiApplication"/> class.
        /// </summary>
        /// <param name="system">The simulated system.</param>
        public GuiApplication(SimulatedSystem system)
            : base(system)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the active session: the first open session; null when none is open.
        /// </summary>
        public object ActiveSession => SimulatedSystem.Sessions.Count == 0 ? null : new GuiSession(SimulatedSystem.Sessions[0]);

        /// <summary>
        /// Gets the connections.
        /// </summary>
        public GuiCollection Children => new(GetChildComponents());

        /// <summary>
        /// Gets the connections, under the name SAP also provides for them.
        /// </summary>
        public GuiCollection Connections => new(GetChildComponents());

        /// <inheritdoc />
        public override bool ContainerType => true;

        /// <inheritdoc />
        public override string Id => "/" + Segment;

        /// <inheritdoc />
        public override object Parent => null;

        /// <inheritdoc />
        internal override string Segment => "app";
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Resolves an absolute or relative identifier.
        /// </summary>
        /// <param name="id">The identifier, such as <c>/app/con[0]/ses[0]</c>.</param>
        /// <param name="raise">True to raise an error when nothing matches; false to return null.</param>
        /// <returns>The matching object; null when nothing matches and <paramref name="raise"/> is false.</returns>
        public object FindById(string id, [Optional, DefaultParameterValue(true)] bool raise)
        {
            return FindComponent(id, raise);
        }

        /// <inheritdoc />
        internal override IReadOnlyList<GuiComponent> GetChildComponents()
        {
            return [new GuiConnection(SimulatedSystem)];
        }
        #endregion
    }
}
