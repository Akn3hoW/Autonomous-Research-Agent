using AutonomousResearchAgent.Application.Concepts;
using AutonomousResearchAgent.Application.Clustering;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class ClusteringService : IClusteringService
{
    private readonly ApplicationDbContext _dbContext;

    public ClusteringService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ClusterMapResult> GetClusterMapAsync(CancellationToken cancellationToken)
    {
        var papers = await _dbContext.Papers
            .AsNoTracking()
            .Where(p => p.ClusterX != null && p.ClusterY != null)
            .Select(p => new PaperWithClusterMap(
                p.Id,
                p.Title,
                p.Abstract,
                p.Authors.AsReadOnly(),
                p.Year,
                p.ClusterX,
                p.ClusterY))
            .ToListAsync(cancellationToken);

        return new ClusterMapResult(papers, papers.Count);
    }

    public async Task<Guid> CreateJobAsync(string? createdBy, CancellationToken cancellationToken)
    {
        var job = new Job
        {
            Type = JobType.GenerateClusterMap,
            Status = JobStatus.Queued,
            PayloadJson = "{}",
            CreatedBy = createdBy
        };

        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
