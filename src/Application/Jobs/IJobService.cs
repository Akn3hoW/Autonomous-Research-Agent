using AutonomousResearchAgent.Application.Common;

namespace AutonomousResearchAgent.Application.Jobs;

public interface IJobService
{
    Task<PagedResult<JobModel>> ListAsync(JobQuery query, CancellationToken cancellationToken);
    Task<JobModel> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<JobModel> CreateAsync(CreateJobCommand command, CancellationToken cancellationToken);
    Task<JobModel> RetryAsync(Guid id, RetryJobCommand command, CancellationToken cancellationToken);
}

