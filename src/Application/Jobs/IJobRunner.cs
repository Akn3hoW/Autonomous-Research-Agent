using AutonomousResearchAgent.Domain.Entities;

namespace AutonomousResearchAgent.Application.Jobs;

public interface IJobRunner
{
    Task RunAsync(Job job, CancellationToken cancellationToken);
}

