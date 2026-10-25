namespace Sap.DriverServer.Inspector.Services
{
    /// <summary>
    /// Provides the tree navigation directions, matching the Accessibility Insights keyboard commands.
    /// </summary>
    /// <remarks>
    /// Both inspection sources resolve the same directions: the local source through SAP GUI's <c>Parent</c> and
    /// <c>Children</c> members, the driver source through the page source tree.
    /// </remarks>
    public static class InspectorNavigation
    {
        #region *** Constants    ***
        /// <summary>
        /// Specifies the first child (Ctrl+Shift+F7).
        /// </summary>
        public const string FirstChild = "firstChild";

        /// <summary>
        /// Specifies the last child (Ctrl+Shift+F9).
        /// </summary>
        public const string LastChild = "lastChild";

        /// <summary>
        /// Specifies the next sibling (Ctrl+Shift+F8).
        /// </summary>
        public const string NextSibling = "nextSibling";

        /// <summary>
        /// Specifies the parent (Ctrl+Shift+F6).
        /// </summary>
        public const string Parent = "parent";

        /// <summary>
        /// Specifies the previous sibling (Ctrl+Shift+F5).
        /// </summary>
        public const string PreviousSibling = "previousSibling";
        #endregion
    }
}
