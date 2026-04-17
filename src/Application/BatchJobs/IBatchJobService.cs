namespace AutonomousResearchAgent.Application.BatchJobs;

public interface IBatchJobService
{
    Task<BatchJobModel> CreateAsync(CreateBatchJobCommand command, CancellationToken cancellationToken);
    Task<BatchJobModel> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<BatchJobModel> UpdateProgressAsync(Guid id, int completed, CancellationToken cancellationToken);
    Task<BatchJobModel> CompleteAsync(Guid id, CancellationToken cancellationToken);
    Task<BatchJobModel> FailAsync(Guid id, string error, CancellationToken cancellationToken);
}

public sealed record BatchJobModel(
    Guid Id,
    Guid UserId,
    string Action,
    string Status,
    int Total,
    int Completed,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateBatchJobCommand(
    string Action,
    Guid UserId,
    int Total);