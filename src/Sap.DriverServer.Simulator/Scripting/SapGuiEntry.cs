using System;
using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates the object SAP Logon registers as <c>SAPGUI</c> in the running object table.
    /// </summary>
    /// <remarks>
    /// The entry's only member is <c>GetScriptingEngine</c>. It returns nothing when the simulator runs with
    /// <c>--scripting-disabled</c>, which is how clients observe client-side disabled scripting.
    /// </remarks>
    [ComVisible(true)]
    public sealed class SapGuiEntry : ApartmentBoundObject
    {
        #region *** Fields       ***
        // Provides the sessions and the scenario switches.
        private readonly SimulatedSystem _system;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="SapGuiEntry"/> class.
        /// </summary>
        /// <param name="system">The simulated system the engine exposes.</param>
        public SapGuiEntry(SimulatedSystem system)
        {
            // Require the system; the engine is created from it on every request.
            ArgumentNullException.ThrowIfNull(argument: system);

            _system = system;
        }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the scripting engine, the <c>GuiApplication</c> object.
        /// </summary>
        /// <returns>The application; null when scripting is disabled.</returns>
        public object GetScriptingEngine()
        {
            // Refuse the engine in the disabled-scripting scenario, as SAP GUI does when the option is off.
            return _system.Options.IsScriptingDisabled ? null : new GuiApplication(_system);
        }
        #endregion
    }
}
