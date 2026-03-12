using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Application.Papers;

public interface IPaperService
{
    Task<PagedResult<PaperListItem>> ListAsync(PaperQuery query, CancellationToken cancellationToken);
    Task<PaperDetail> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<PaperDetail> CreateAsync(CreatePaperCommand command, CancellationToken cancellationToken);
    Task<PaperDetail> UpdateAsync(Guid id, UpdatePaperCommand command, CancellationToken cancellationToken);
    Task<ImportPapersResult> ImportAsync(ImportPapersCommand command, CancellationToken cancellationToken);
}

