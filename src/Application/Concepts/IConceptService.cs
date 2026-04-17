using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Concepts;

public interface IConceptService
{
    Task<PagedResult<ConceptModel>> ListAsync(ConceptQuery query, CancellationToken cancellationToken);
    Task<ConceptStatistics> GetStatisticsAsync(CancellationToken cancellationToken);
    Task<Guid> CreateJobAsync(string? createdBy, CancellationToken cancellationToken);
}
