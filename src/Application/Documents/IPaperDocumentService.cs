namespace AutonomousResearchAgent.Application.Documents;

public interface IPaperDocumentService
{
    Task<IReadOnlyCollection<PaperDocumentModel>> ListByPaperIdAsync(Guid paperId, CancellationToken cancellationToken);
    Task<PaperDocumentModel> GetByIdAsync(Guid paperId, Guid documentId, CancellationToken cancellationToken);
    Task<PaperDocumentModel> CreateAsync(CreatePaperDocumentCommand command, CancellationToken cancellationToken);
    Task<PaperDocumentModel> QueueProcessingAsync(Guid paperId, Guid documentId, QueuePaperDocumentProcessingCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid paperId, Guid documentId, CancellationToken cancellationToken);
}
