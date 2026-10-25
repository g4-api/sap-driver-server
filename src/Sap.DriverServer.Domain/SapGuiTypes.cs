/*
 * RESOURCES
 * https://help.sap.com/docs/sap_gui_for_windows/b47d018c3b9b45e897faf66a6c0885a8/babdf65f4d0a4bd8b40f5ff132cb12fa.html
 */
namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Provides the SAP GUI Scripting <c>Type</c> values the driver treats specially.
    /// </summary>
    /// <remarks>
    /// The values are the strings returned by the <c>Type</c> property and are used as element names in the
    /// SAP object model document, so XPath expressions such as <c>//GuiButton</c> match them directly.
    /// </remarks>
    public static class SapGuiTypes
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies a push button; click invokes <c>press</c>.
        /// </summary>
        public const string Button = "GuiButton";

        /// <summary>
        /// Specifies a check box; click toggles <c>selected</c>.
        /// </summary>
        public const string CheckBox = "GuiCheckBox";

        /// <summary>
        /// Specifies a combo box; send keys sets <c>key</c> to the entry key.
        /// </summary>
        public const string ComboBox = "GuiComboBox";

        /// <summary>
        /// Specifies a text field with an input help (F4).
        /// </summary>
        public const string ContextTextField = "GuiCTextField";

        /// <summary>
        /// Specifies a label; click sets the focus, which is how list screens select a line.
        /// </summary>
        public const string Label = "GuiLabel";

        /// <summary>
        /// Specifies a menu entry; click invokes <c>select</c>.
        /// </summary>
        public const string Menu = "GuiMenu";

        /// <summary>
        /// Specifies the transaction code (OK code) field in the system toolbar.
        /// </summary>
        public const string OkCodeField = "GuiOkCodeField";

        /// <summary>
        /// Specifies a password field; the driver never reads its text.
        /// </summary>
        public const string PasswordField = "GuiPasswordField";

        /// <summary>
        /// Specifies a radio button; click invokes <c>select</c>.
        /// </summary>
        public const string RadioButton = "GuiRadioButton";

        /// <summary>
        /// Specifies a scripting session.
        /// </summary>
        public const string Session = "GuiSession";

        /// <summary>
        /// Specifies an ActiveX shell control (grid, tree, text edit, toolbar, and others), distinguished by <c>SubType</c>.
        /// </summary>
        public const string Shell = "GuiShell";

        /// <summary>
        /// Specifies a tab strip tab; click invokes <c>select</c>.
        /// </summary>
        public const string Tab = "GuiTab";

        /// <summary>
        /// Specifies the <c>SubType</c> of a multi-line text edit shell, whose <c>text</c> property is writable.
        /// </summary>
        public const string TextEditSubType = "TextEdit";

        /// <summary>
        /// Specifies a single-line text field.
        /// </summary>
        public const string TextField = "GuiTextField";
        #endregion
    }
}
