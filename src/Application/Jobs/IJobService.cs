using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Jobs;

public interface IJobService
{
    Task<PagedResult<JobModel>> ListAsync(JobQuery query, CancellationToken cancellationToken);
    Task<JobModel> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<JobModel> CreateAsync(CreateJobCommand command, CancellationToken cancellationToken);
    Task<JobModel> RetryAsync(Guid id, RetryJobCommand command, CancellationToken cancellationToken);
    Task<JobModel> UpdateRetryStatusAsync(Guid id, int retryCount, string retryPolicyJson, CancellationToken cancellationToken);
    Task<JobModel> UpdateStatusAsync(Guid id, JobStatus status, string? errorMessage, CancellationToken cancellationToken);
    Task<JobModel> CancelAsync(Guid id, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<List<JobModel>> GetJobsByParentIdAsync(Guid parentId, CancellationToken cancellationToken);
}
