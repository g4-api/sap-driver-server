using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiCollection</c> and <c>GuiComponentCollection</c>: an indexed, read-only list.
    /// </summary>
    /// <remarks>
    /// The collection is a snapshot taken when it was requested; later screen changes do not update it, which matches
    /// how scripts are expected to re-read <c>Children</c> after a screen change.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiCollection : ApartmentBoundObject
    {
        #region *** Fields       ***
        // Holds the items: scripting objects or scalars, such as findByPosition's identifier and inner object.
        private readonly List<object> _items;
        #endregion

        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiCollection"/> class.
        /// </summary>
        /// <param name="items">The items in order; null creates an empty collection.</param>
        public GuiCollection(IEnumerable<object> items)
        {
            _items = items?.ToList() ?? [];
        }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets the number of items.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Gets the number of items, under the name VBScript clients use.
        /// </summary>
        public int Length => _items.Count;

        /// <summary>
        /// Gets the SAP type name.
        /// </summary>
        public string Type => nameof(GuiCollection);
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the item at a zero-based index.
        /// </summary>
        /// <param name="index">The zero-based index.</param>
        /// <returns>The item.</returns>
        public object ElementAt(int index)
        {
            return _items[index];
        }

        /// <summary>
        /// Gets the item at a zero-based index, under the default-member name VBScript clients use.
        /// </summary>
        /// <param name="index">The zero-based index.</param>
        /// <returns>The item.</returns>
        public object Item(int index)
        {
            return _items[index];
        }
        #endregion
    }
}
