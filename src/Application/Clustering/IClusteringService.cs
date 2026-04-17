using AutonomousResearchAgent.Application.Concepts;

namespace AutonomousResearchAgent.Application.Clustering;

public interface IClusteringService
{
    Task<ClusterMapResult> GetClusterMapAsync(CancellationToken cancellationToken);
    Task<Guid> CreateJobAsync(string? createdBy, CancellationToken cancellationToken);
}
