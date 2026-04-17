using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Watchlist;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class SavedSearchService(
    ApplicationDbContext dbContext,
    ISemanticScholarClient semanticScholarClient,
    IJobService jobService,
    ILogger<SavedSearchService> logger) : ISavedSearchService
{
    public async Task<PagedResult<SavedSearchModel>> ListAsync(SavedSearchQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var savedSearchesQuery = dbContext.SavedSearches.AsNoTracking().AsQueryable();

        if (query.UserId.HasValue)
        {
            savedSearchesQuery = savedSearchesQuery.Where(s => s.UserId == query.UserId.Value);
        }

        if (query.IsActive.HasValue)
        {
            savedSearchesQuery = savedSearchesQuery.Where(s => s.IsActive == query.IsActive.Value);
        }

        var totalCount = await savedSearchesQuery.LongCountAsync(cancellationToken);

        var entityIds = await savedSearchesQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var resultCounts = await dbContext.Jobs
            .Where(j => entityIds.Contains(j.TargetEntityId ?? Guid.Empty) &&
                        j.Status == JobStatus.Completed)
            .GroupBy(j => j.TargetEntityId)
            .Select(g => new { TargetEntityId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var resultCountMap = resultCounts.ToDictionary(r => r.TargetEntityId ?? Guid.Empty, r => r.Count);

        var entities = await savedSearchesQuery
            .OrderByDescending(s => s.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(e => ToModel(e, resultCountMap.GetValueOrDefault(e.Id, 0))).ToList();

        return new PagedResult<SavedSearchModel>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<SavedSearchModel> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.SavedSearches.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SavedSearch), id);

        var resultCount = await dbContext.Jobs
            .Where(j => j.TargetEntityId == id && j.Status == JobStatus.Completed)
            .CountAsync(cancellationToken);

        return ToModel(entity, resultCount);
    }

    public async Task<SavedSearchModel> CreateAsync(CreateSavedSearchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = new SavedSearch
        {
            UserId = command.UserId,
            Query = command.Query.Trim(),
            Field = command.Field?.Trim(),
            Schedule = command.Schedule,
            IsActive = true
        };

        dbContext.SavedSearches.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created saved search {SavedSearchId} for user {UserId}", entity.Id, entity.UserId);
        return ToModel(entity, 0);
    }

    public async Task<SavedSearchModel> UpdateAsync(Guid id, UpdateSavedSearchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = await dbContext.SavedSearches.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SavedSearch), id);

        if (command.Query is not null)
        {
            entity.Query = command.Query.Trim();
        }

        if (command.Field is not null)
        {
            entity.Field = string.IsNullOrWhiteSpace(command.Field) ? null : command.Field.Trim();
        }

        if (command.Schedule.HasValue)
        {
            entity.Schedule = command.Schedule.Value;
        }

        if (command.IsActive.HasValue)
        {
            entity.IsActive = command.IsActive.Value;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var resultCount = await dbContext.Jobs
            .Where(j => j.TargetEntityId == id && j.Status == JobStatus.Completed)
            .CountAsync(cancellationToken);

        logger.LogInformation("Updated saved search {SavedSearchId}", entity.Id);
        return ToModel(entity, resultCount);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.SavedSearches.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SavedSearch), id);

        dbContext.SavedSearches.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted saved search {SavedSearchId}", id);
    }

    public async Task<RunSavedSearchResult> RunAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.SavedSearches.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SavedSearch), id);

        var queries = new List<string> { entity.Query };
        if (!string.IsNullOrWhiteSpace(entity.Field))
        {
            queries = new List<string> { $"{entity.Query} {entity.Field}" };
        }

        var imported = await semanticScholarClient.SearchPapersAsync(queries, 10, cancellationToken);

        var semanticIds = imported.Select(c => c.SemanticScholarId).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var dois = imported.Select(c => c.Doi).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        var existingPapers = await dbContext.Papers
            .Where(p => (p.SemanticScholarId != null && semanticIds.Contains(p.SemanticScholarId))
                     || (p.Doi != null && dois.Contains(p.Doi)))
            .CountAsync(cancellationToken);

        var newCount = imported.Count - existingPapers;
        if (newCount < 0) newCount = 0;

        var job = await jobService.CreateAsync(
            new CreateJobCommand(JobType.ImportPapers,
                new JsonObject
                {
                    ["queries"] = new JsonArray { JsonValue.Create(entity.Query) },
                    ["limit"] = 10,
                    ["storeImportedPapers"] = true
                },
                id,
                $"saved-search:{entity.Id}"),
            cancellationToken);

        entity.LastRunAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Ran saved search {SavedSearchId}, found {NewCount} new papers", id, newCount);
        return new RunSavedSearchResult(newCount, job.Id);
    }

    private static SavedSearchModel ToModel(SavedSearch entity, int resultCount) =>
        new(
            entity.Id,
            entity.UserId,
            entity.Query,
            entity.Field,
            entity.Schedule,
            entity.LastRunAt,
            resultCount,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
}
