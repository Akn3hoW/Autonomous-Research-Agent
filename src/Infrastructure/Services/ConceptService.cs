using System;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Concepts;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class ConceptService : IConceptService
{
    private readonly ApplicationDbContext _dbContext;

    public ConceptService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<ConceptModel>> ListAsync(ConceptQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = _dbContext.PaperConcepts.AsNoTracking();

        if (query.ConceptType.HasValue)
        {
            q = q.Where(c => c.ConceptType == query.ConceptType.Value);
        }

        if (query.PaperId.HasValue)
        {
            q = q.Where(c => c.PaperId == query.PaperId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            q = q.Where(c => c.Name.ToLower().Contains(search));
        }

        var totalCount = await q.LongCountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.Name)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new ConceptModel(
                c.Id,
                c.PaperId,
                c.ConceptType,
                c.Name,
                c.Confidence,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ConceptModel>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<ConceptStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        var byType = await _dbContext.PaperConcepts
            .AsNoTracking()
            .GroupBy(c => c.ConceptType)
            .Select(g => new ConceptTypeCount(
                g.Key,
                g.Count(),
                g.Select(c => c.PaperId).Distinct().Count()))
            .ToListAsync(cancellationToken);

        var totalCount = await _dbContext.PaperConcepts.LongCountAsync(cancellationToken);
        var uniquePaperCount = await _dbContext.PaperConcepts.Select(c => c.PaperId).Distinct().CountAsync(cancellationToken);

        return new ConceptStatistics(byType, (int)totalCount, uniquePaperCount);
    }

    public async Task<Guid> CreateJobAsync(string? createdBy, CancellationToken cancellationToken)
    {
        var job = new Job
        {
            Type = JobType.ExtractConcepts,
            Status = JobStatus.Queued,
            PayloadJson = "{}",
            CreatedBy = createdBy
        };

        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return job.Id;
    }
}
