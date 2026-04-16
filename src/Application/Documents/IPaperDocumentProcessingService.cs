using AutonomousResearchAgent.Domain.Entities;

namespace AutonomousResearchAgent.Application.Documents;

public interface IPaperDocumentProcessingService
{
    Task<PaperDocument> ProcessAsync(Guid documentId, CancellationToken cancellationToken);
}