using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiComponent</c>: the identity every scripting object shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Scripting objects are thin, short-lived views over the simulation: they hold no state of their own and read the
    /// WPF elements on every call, so an object a client keeps sees the screen as it is now. Each object is identified
    /// by one identifier segment, such as <c>wnd[0]</c> or <c>txtRSYST-BNAME</c>; <see cref="Id"/> joins the segments
    /// from the application down.
    /// </para>
    /// <para>
    /// Lookups compare segments case-insensitively. A failed lookup either returns null or raises an error, depending on
    /// the caller's <c>raise</c> argument, as <c>findById</c> does in SAP GUI.
    /// </para>
    /// </remarks>
    [ComVisible(true)]
    public abstract class GuiComponent : ApartmentBoundObject
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiComponent"/> class.
        /// </summary>
        /// <param name="system">The simulated system, used to resolve absolute identifiers.</param>
        protected GuiComponent(SimulatedSystem system)
        {
            // Require the system; absolute identifiers resolve from its application.
            ArgumentNullException.ThrowIfNull(argument: system);

            SimulatedSystem = system;
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets a value indicating whether the object has children.
        /// </summary>
        public virtual bool ContainerType => false;

        /// <summary>
        /// Gets the absolute identifier, such as <c>/app/con[0]/ses[0]/wnd[0]/usr/btnOPEN_POPUP</c>.
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// Gets the name: the segment without its type prefix, or the prefix alone for indexed segments such as <c>btn[3]</c>.
        /// </summary>
        public string Name => ConvertToName(Segment);

        /// <summary>
        /// Gets the parent object; null for the application.
        /// </summary>
        public abstract object Parent { get; }

        /// <summary>
        /// Gets the SAP type name, such as <c>GuiButton</c>.
        /// </summary>
        public virtual string Type => GetType().Name;

        /// <summary>
        /// Gets the identifier segment, such as <c>wnd[0]</c>.
        /// </summary>
        internal abstract string Segment { get; }

        /// <summary>
        /// Gets the simulated system.
        /// </summary>
        internal SimulatedSystem SimulatedSystem { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Resolves an absolute or relative identifier, as SAP's <c>findById</c> does.
        /// </summary>
        /// <param name="id">The identifier; absolute identifiers start with <c>/app</c>.</param>
        /// <param name="raise">True to raise an error when nothing matches; false to return null.</param>
        /// <returns>The matching object, or null when nothing matches and <paramref name="raise"/> is false.</returns>
        /// <exception cref="SimulatorException">Nothing matches and <paramref name="raise"/> is true.</exception>
        internal GuiComponent FindComponent(string id, bool raise)
        {
            // Resolve absolute identifiers from the application, relative ones from this object.
            var segments = (id ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
            var isAbsolute = id?.StartsWith('/') == true;
            var current = isAbsolute ? (GuiComponent)new GuiApplication(SimulatedSystem) : this;
            var start = isAbsolute ? 1 : 0;
            var isRootMatch = !isAbsolute || (segments.Length > 0 && string.Equals(segments[0], current.Segment, StringComparison.OrdinalIgnoreCase));

            // Walk one segment at a time through each level's children.
            for (var index = start; isRootMatch && current is not null && index < segments.Length; index++)
            {
                var segment = segments[index];
                current = current
                    .GetChildComponents()
                    .FirstOrDefault(i => string.Equals(i.Segment, segment, StringComparison.OrdinalIgnoreCase));
            }

            var result = isRootMatch && segments.Length > 0 ? current : null;
            if (result is null && raise)
            {
                throw new SimulatorException($"The control could not be found by id '{id}'.");
            }

            return result;
        }

        /// <summary>
        /// Gets the scripting children.
        /// </summary>
        /// <returns>The children in SAP order; empty for objects that are not containers.</returns>
        internal virtual IReadOnlyList<GuiComponent> GetChildComponents()
        {
            return [];
        }

        // Derives SAP's Name from a segment: the index is dropped, and so is the type prefix unless nothing else remains.
        private static string ConvertToName(string segment)
        {
            // Drop the index, as in "btn[3]" to "btn" and "txtITAB-MATNR[1,4]" to "txtITAB-MATNR".
            var bracket = segment.IndexOf('[', StringComparison.Ordinal);
            var name = bracket > 0 ? segment[..bracket] : segment;

            // Drop the type prefix, as in "txtRSYST-BNAME" to "RSYST-BNAME"; "btn", "wnd", and unknown prefixes stay whole.
            var prefix = GuiComponentFactory.GetPrefix(name);

            return prefix.Length > 0 && prefix.Length < name.Length ? name[prefix.Length..] : name;
        }
        #endregion
    }
}
