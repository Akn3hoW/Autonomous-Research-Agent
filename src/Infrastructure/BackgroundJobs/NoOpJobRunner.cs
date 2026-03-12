using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.BackgroundJobs;

public sealed class NoOpJobRunner(ILogger<NoOpJobRunner> logger) : IJobRunner
{
    public Task RunAsync(Job job, CancellationToken cancellationToken)
    {
        logger.LogInformation("No-op job runner invoked for job {JobId} ({JobType})", job.Id, job.Type);
        return Task.CompletedTask;
    }
}

