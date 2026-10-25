using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Simulates SAP's <c>GuiComboBox</c>: a drop-down list whose entries have a key and a value (<c>cmb</c>).
    /// </summary>
    /// <remarks>
    /// Each entry is a combo box item whose tag is the key and whose content is the value. Setting a key or value that
    /// no entry has raises an error, as in SAP GUI.
    /// </remarks>
    [ComVisible(true)]
    public sealed class GuiComboBox : GuiVComponent
    {
        #region *** Constructors ***
        /// <summary>
        /// Initializes a new instance of the <see cref="GuiComboBox"/> class.
        /// </summary>
        /// <param name="element">The combo box.</param>
        public GuiComboBox(FrameworkElement element)
            : base(element)
        { }
        #endregion

        #region *** Properties   ***
        /// <summary>
        /// Gets or sets the key of the selected entry, such as <c>EUR</c>.
        /// </summary>
        public string Key
        {
            get => (ComboBox.SelectedItem as ComboBoxItem)?.Tag as string ?? string.Empty;
            set => Select(i => string.Equals(i.Tag as string, value, StringComparison.Ordinal), value);
        }

        /// <summary>
        /// Gets or sets the value of the selected entry, such as <c>Euro</c>.
        /// </summary>
        public string Value
        {
            get => (ComboBox.SelectedItem as ComboBoxItem)?.Content as string ?? string.Empty;
            set => Select(i => string.Equals(i.Content as string, value, StringComparison.Ordinal), value);
        }

        // Gets the wrapped combo box.
        private ComboBox ComboBox => (ComboBox)Element;
        #endregion

        #region *** Methods      ***
        /// <inheritdoc />
        protected override string GetText()
        {
            return Value;
        }

        // Selects the first entry that matches, or raises an error naming the requested key or value.
        private void Select(Func<ComboBoxItem, bool> isMatch, string requested)
        {
            // Find the entry first, so a missing key or value leaves the selection unchanged.
            var entry = ComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(isMatch);
            if (entry is null)
            {
                throw new SimulatorException($"The combo box '{Id}' has no entry '{requested}'.");
            }

            ComboBox.SelectedItem = entry;
        }
        #endregion
    }
}
