namespace Sap.DriverServer.Simulator.Views
{
    /// <summary>
    /// Defines the content of a popup (<c>wnd[1]</c>): its title, its toolbar, and what Enter does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ModalFrameWindow"/> hosts the content and owns the popup toolbar (<c>tbar[0]</c>). The toolbar's
    /// <c>btn[0]</c> and Enter both run <see cref="Confirm"/>; its <c>btn[12]</c>, F12, and Escape close the popup
    /// without an answer when <see cref="IsCancelable"/> is true. The content marks its own user area (<c>usr</c>).
    /// </para>
    /// <para>
    /// Implementations answer through the session, which closes the popup and reports the outcome in the status bar.
    /// </para>
    /// </remarks>
    public interface IPopupScreen
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the text of the toolbar's confirm button (<c>btn[0]</c>); null when the popup has no toolbar.
        /// </summary>
        string ConfirmText { get; }

        /// <summary>
        /// Gets a value indicating whether the popup can be closed without an answer, by F12, Escape, or <c>btn[12]</c>.
        /// </summary>
        bool IsCancelable { get; }

        /// <summary>
        /// Gets the popup title SAP GUI reports, such as <c>Information</c>.
        /// </summary>
        string Title { get; }
        #endregion

        #region *** Methods      ***
        /// <summary>
        /// Runs the popup's default action, as Enter and the confirm button do.
        /// </summary>
        void Confirm();
        #endregion
    }
}
