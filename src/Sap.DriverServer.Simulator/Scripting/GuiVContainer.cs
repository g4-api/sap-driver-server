using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiVContainer</c>: a visible control that contains other controls.
    /// </summary>
    /// <remarks>
    /// The children are the element's nearest marked descendants, read again on every call.
    /// </remarks>
    [ComVisible(true)]
    public abstract class GuiVContainer : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiVContainer"/> class.
        /// </summary>
        /// <param name="element">The WPF element.</param>
        protected GuiVContainer(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the direct children.
        /// </summary>
        public GuiCollection Children => new(GetChildComponents());

        /// <inheritdoc />
        public override bool ContainerType => true;
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Resolves an absolute identifier, or one relative to this container.
        /// </summary>
        /// <param name="id">The identifier, such as <c>usr/txtRSYST-BNAME</c>.</param>
        /// <param name="raise">True to raise an error when nothing matches; false to return null.</param>
        /// <returns>The matching object; null when nothing matches and <paramref name="raise"/> is false.</returns>
        public object FindById(string id, [Optional, DefaultParameterValue(true)] bool raise)
        {
            return FindComponent(id, raise);
        }

        /// <inheritdoc />
        internal override IReadOnlyList<GuiComponent> GetChildComponents()
        {
            // Wrap each marked child element in the scripting type its identifier prefix selects.
            return ScriptingTree
                .GetChildElements(Element)
                .Select(i => (GuiComponent)GuiComponentFactory.NewComponent(i))
                .ToList();
        }
        #endregion
    }
}
