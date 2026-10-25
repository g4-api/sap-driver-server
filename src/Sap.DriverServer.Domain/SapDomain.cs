namespace Sap.DriverServer.Domain
{
    /// <summary>
    /// Default <see cref="ISapDomain"/> that exposes the injected repositories.
    /// </summary>
    /// <param name="actionsRepository">The actions repository.</param>
    /// <param name="documentRepository">The document repository.</param>
    /// <param name="elementsRepository">The elements repository.</param>
    /// <param name="sessionsRepository">The sessions repository.</param>
    public class SapDomain(
        IActionsRepository actionsRepository,
        IDocumentRepository documentRepository,
        IElementsRepository elementsRepository,
        ISessionsRepository sessionsRepository) : ISapDomain
    {
        #region *** Properties   ***
        /// <inheritdoc />
        public IActionsRepository ActionsRepository { get; } = actionsRepository;

        /// <inheritdoc />
        public IDocumentRepository DocumentRepository { get; } = documentRepository;

        /// <inheritdoc />
        public IElementsRepository ElementsRepository { get; } = elementsRepository;

        /// <inheritdoc />
        public ISessionsRepository SessionsRepository { get; } = sessionsRepository;
        #endregion
    }
}
