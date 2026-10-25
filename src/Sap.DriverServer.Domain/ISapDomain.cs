namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Groups the SAP driver repositories for injection into controllers and middleware, mirroring <c>IUiaDomain</c>.
    /// </summary>
    public interface ISapDomain
    {
        #region *** Properties   ***
        /// <summary>
        /// Gets the actions repository.
        /// </summary>
        IActionsRepository ActionsRepository { get; }

        /// <summary>
        /// Gets the document repository.
        /// </summary>
        IDocumentRepository DocumentRepository { get; }

        /// <summary>
        /// Gets the elements repository.
        /// </summary>
        IElementsRepository ElementsRepository { get; }

        /// <summary>
        /// Gets the sessions repository.
        /// </summary>
        ISessionsRepository SessionsRepository { get; }
        #endregion
    }
}
