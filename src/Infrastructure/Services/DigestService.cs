using System.Text.Json;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Watchlist;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class DigestService(
    ApplicationDbContext dbContext,
    IOpenRouterChatClient openRouterChatClient,
    ISemanticScholarClient semanticScholarClient,
    ILogger<DigestService> logger) : IDigestService
{
    public async Task<DigestModel> CreateDigestAsync(CreateDigestCommand command, CancellationToken cancellationToken)
    {
        var entity = new Digest
        {
            UserId = command.UserId,
            Frequency = command.Frequency,
            Topic = command.Topic,
            Content = command.Content,
            NewPapersCount = command.NewPapersCount
        };

        dbContext.Digests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created digest {DigestId} for user {UserId}", entity.Id, entity.UserId);
        return ToModel(entity);
    }

    public async Task<DigestModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Digests
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        return entity != null ? ToModel(entity) : null;
    }

    public async Task<IReadOnlyList<DigestModel>> GetDigestsForUserAsync(Guid userId, DigestFrequency? frequency, CancellationToken cancellationToken)
    {
        var query = dbContext.Digests
            .AsNoTracking()
            .Where(d => d.UserId == userId);

        if (frequency.HasValue)
        {
            query = query.Where(d => d.Frequency == frequency.Value);
        }

        var entities = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return entities.Select(ToModel).ToList();
    }

    public async Task<DigestModel?> GetLatestDigestAsync(Guid userId, DigestFrequency frequency, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Digests
            .AsNoTracking()
            .Where(d => d.UserId == userId && d.Frequency == frequency)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity != null ? ToModel(entity) : null;
    }

    public async Task<string> GenerateDigestContentAsync(Guid userId, DigestFrequency frequency, CancellationToken cancellationToken)
    {
        var savedSearches = await dbContext.SavedSearches
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync(cancellationToken);

        var newPapersList = new List<object>();
        var totalNewPapers = 0;

        foreach (var search in savedSearches)
        {
            var queries = new List<string> { search.Query };
            if (!string.IsNullOrWhiteSpace(search.Field))
            {
                queries = new List<string> { $"{search.Query} {search.Field}" };
            }

            var results = await semanticScholarClient.SearchPapersAsync(queries, 20, cancellationToken);

            var semanticIds = results.Select(r => r.SemanticScholarId).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var dois = results.Select(r => r.Doi).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

            var existingPapers = await dbContext.Papers
                .Where(p => (p.SemanticScholarId != null && semanticIds.Contains(p.SemanticScholarId))
                         || (p.Doi != null && dois.Contains(p.Doi)))
                .Select(p => p.SemanticScholarId ?? p.Doi)
                .ToListAsync(cancellationToken);

            var existingIds = existingPapers.Where(p => p != null).Select(p => p!).ToHashSet();

            var newPapers = results
                .Where(r => !string.IsNullOrWhiteSpace(r.SemanticScholarId) && !existingIds.Contains(r.SemanticScholarId!) ||
                            !string.IsNullOrWhiteSpace(r.Doi) && !existingIds.Contains(r.Doi!))
                .ToList();

            totalNewPapers += newPapers.Count;

            foreach (var paper in newPapers)
            {
                newPapersList.Add(new
                {
                    title = paper.Title,
                    authors = paper.Authors,
                    year = paper.Year,
                    venue = paper.Venue,
                    searchQuery = search.Query
                });
            }
        }

        if (newPapersList.Count == 0)
        {
            return $"No new papers found for your {frequency} digest.";
        }

        var systemPrompt = "You are an expert research analyst creating a digest of new papers. Return valid JSON only.";
        var userPrompt = $@"Create a {frequency} digest summary for these new papers found since the last digest.

NEW PAPERS:
{JsonSerializer.Serialize(newPapersList.Take(20))}

Provide a JSON summary with:
- summary: A concise paragraph summarizing the key themes and trends
- highlights: Array of 3-5 most important papers with why they're significant
- topics: Array of main research topics covered
- suggestedActions: Array of suggested follow-up actions";

        var result = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

        return result?.ToJsonString() ?? JsonSerializer.Serialize(new { summary = "Digest generated", papers = newPapersList.Count });
    }

    private static DigestModel ToModel(Digest entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.Frequency,
            entity.Topic,
            entity.Content,
            entity.NewPapersCount,
            entity.CreatedAt);
}