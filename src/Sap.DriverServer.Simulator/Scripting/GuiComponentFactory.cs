using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using Sap.DriverServer.Simulator.Simulation;
using Sap.DriverServer.Simulator.Views;

namespace Sap.DriverServer.Simulator.Scripting
{
    /// <summary>
    /// Creates the scripting object for a WPF element from the type prefix of its identifier segment.
    /// </summary>
    /// <remarks>
    /// The prefixes are SAP GUI's, such as <c>txt</c> for <c>GuiTextField</c> and <c>btn</c> for <c>GuiButton</c>.
    /// Longer prefixes are listed before shorter ones that start the same way, such as <c>shellcont</c> before
    /// <c>shell</c>. A <c>shell</c> element's type comes from its WPF element: a grid view, a text edit, a toolbar,
    /// a picture, a splitter, or a tree.
    /// </remarks>
    internal static class GuiComponentFactory
    {
        #region *** Constants    ***
        // Maps each identifier prefix to the scripting type that represents it.
        private static readonly (string Prefix, Func<FrameworkElement, GuiVComponent> Create)[] Factories =
        [
            ("shellcont", i => new GuiContainerShell(i)),
            ("shell", NewShell),
            ("ctxt", i => new GuiCTextField(i)),
            ("txt", i => new GuiTextField(i)),
            ("pwd", i => new GuiPasswordField(i)),
            ("okcd", i => new GuiOkCodeField(i)),
            ("btn", i => new GuiButton(i)),
            ("chk", i => new GuiCheckBox(i)),
            ("rad", i => new GuiRadioButton(i)),
            ("cmb", i => new GuiComboBox(i)),
            ("lbl", i => new GuiLabel(i)),
            ("tabs", i => new GuiTabStrip(i)),
            ("tbl", i => new GuiTableControl(i)),
            ("tabp", i => new GuiTab(i)),
            ("tbar", i => new GuiToolbar(i)),
            ("titl", i => new GuiTitlebar(i)),
            ("usr", i => new GuiUserArea(i)),
            ("sbar", i => new GuiStatusbar(i)),
            ("ssub", i => new GuiScrollContainer(i)),
            ("sub", i => new GuiSimpleContainer(i)),
            ("box", i => new GuiBox(i)),
            ("cntl", i => new GuiCustomControl(i)),
            ("mbar", i => new GuiMenubar(i)),
            ("menu", i => new GuiMenu(i))
        ];
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Gets the type prefix of an identifier segment.
        /// </summary>
        /// <param name="segment">The segment, such as <c>txtRSYST-BNAME</c>.</param>
        /// <returns>The prefix, such as <c>txt</c>; empty when the segment has no known prefix.</returns>
        public static string GetPrefix(string segment)
        {
            return Factories.FirstOrDefault(i => segment.StartsWith(i.Prefix, StringComparison.Ordinal)).Prefix ?? string.Empty;
        }

        /// <summary>
        /// Creates the scripting object for a marked element.
        /// </summary>
        /// <param name="element">The element; its identifier segment selects the type.</param>
        /// <returns>The scripting object.</returns>
        /// <exception cref="SimulatorException">The segment has no known prefix, which is a screen authoring error.</exception>
        public static GuiVComponent NewComponent(FrameworkElement element)
        {
            // Pick the factory by prefix; an unknown prefix means the screen marked an element incorrectly.
            var segment = ScriptingTree.GetId(element) ?? string.Empty;
            var factory = Factories.FirstOrDefault(i => segment.StartsWith(i.Prefix, StringComparison.Ordinal)).Create;
            if (factory is null)
            {
                var message = $"The identifier segment '{segment}' has no SAP type prefix.";
                throw new SimulatorException(message);
            }

            return factory(element);
        }

        /// <summary>
        /// Creates the scripting object for a frame window: <c>GuiMainWindow</c> for <c>wnd[0]</c>, <c>GuiModalWindow</c> otherwise.
        /// </summary>
        /// <param name="frame">The simulator window.</param>
        /// <returns>The scripting object.</returns>
        public static GuiFrameWindow NewWindow(FrameWindow frame)
        {
            return frame.WindowIndex == 0 ? new GuiMainWindow(frame) : new GuiModalWindow(frame);
        }

        // Creates a shell by its WPF element: grid view, text edit, toolbar, picture, splitter, or tree.
        private static GuiVComponent NewShell(FrameworkElement element)
        {
            return element switch
            {
                DataGrid => new GuiGridView(element),
                TextBox => new GuiTextedit(element),
                ShellToolbar => new GuiToolbarControl(element),
                PictureHost => new GuiPicture(element),
                SplitterHost => new GuiSplit(element),
                _ => new GuiTree(element)
            };
        }
        #endregion
    }
}
