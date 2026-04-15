using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Application.ReadingSessions;

public interface IReadingSessionService
{
    Task<PagedResult<ReadingSessionModel>> ListAsync(ReadingSessionQuery query, CancellationToken cancellationToken = default);
    Task<ReadingSessionModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReadingSessionModel> CreateAsync(CreateReadingSessionCommand command, CancellationToken cancellationToken = default);
    Task<ReadingSessionModel> UpdateAsync(Guid id, UpdateReadingSessionCommand command, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
