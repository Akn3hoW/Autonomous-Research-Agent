using System.Text.Json;
using System.Text.RegularExpressions;
using AutonomousResearchAgent.Application.Admin;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class AnalyticsService(ApplicationDbContext dbContext) : IAnalyticsService
{
    private static readonly string[] StopWords =
    [
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with",
        "by", "from", "as", "is", "was", "are", "were", "been", "be", "have", "has", "had",
        "do", "does", "did", "will", "would", "could", "should", "may", "might", "must",
        "shall", "can", "need", "dare", "ought", "used", "it", "its", "this", "that",
        "these", "those", "i", "you", "he", "she", "we", "they", "what", "which", "who",
        "whom", "whose", "where", "when", "why", "how", "all", "each", "every", "both",
        "few", "more", "most", "other", "some", "such", "no", "nor", "not", "only", "own",
        "same", "so", "than", "too", "very", "just", "also", "now", "here", "there",
        "then", "once", "if", "because", "until", "while", "although", "though", "after",
        "before", "since", "when", "where", "unless", "about", "into", "through", "during",
        "above", "below", "between", "under", "again", "further", "then", "once", "any",
        "our", "out", "up", "down", "over", "under", "new", "use", "based", "using",
        "method", "methods", "result", "results", "paper", "papers", "show", "shows",
        "shown", "presented", "proposed", "propose", "approach", "approaches", "work",
        "works", "well", "one", "two", "first", "second", "different", "various",
        "many", "several", "given", "however", "therefore", "thus", "hence", "et al",
        "ie", "eg", "vs", "via", "per", "among", "within", "along", "across", "based"
    ];

    public async Task<AnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var papers = dbContext.Papers.AsNoTracking();
        var jobs = dbContext.Jobs.AsNoTracking();

        var totalPapers = await papers.CountAsync(cancellationToken);

        var twelveMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-12);
        var papersOverTime = await papers
            .Where(p => p.CreatedAt >= twelveMonthsAgo)
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToListAsync(cancellationToken);

        var papersOverTimeDtos = papersOverTime
            .Select(x => new PaperOverTimeDto($"{x.Year}-{x.Month:D2}", x.Count))
            .ToList();

        var papersBySource = await papers
            .GroupBy(p => p.Source)
            .Select(g => new PaperSourceCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

        var papersByStatus = await papers
            .GroupBy(p => p.Status)
            .Select(g => new PaperStatusCountDto(g.Key.ToString(), g.Count()))
            .ToListAsync(cancellationToken);

        var papersByVenue = await papers
            .Where(p => !string.IsNullOrEmpty(p.Venue))
            .GroupBy(p => p.Venue!)
            .Select(g => new PaperVenueCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(20)
            .ToListAsync(cancellationToken);

        var papersByYear = await papers
            .Where(p => p.Year.HasValue)
            .GroupBy(p => p.Year!.Value)
            .Select(g => new PaperYearCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Year)
            .ToListAsync(cancellationToken);

        var twelveMonthsJobs = await jobs
            .Where(j => j.CreatedAt >= twelveMonthsAgo)
            .ToListAsync(cancellationToken);

        var jobThroughput = papersOverTime
            .Select(x =>
            {
                var monthStart = new DateTimeOffset(x.Year, x.Month, 1, 0, 0, 0, TimeSpan.Zero);
                var monthEnd = monthStart.AddMonths(1);
                var monthJobs = twelveMonthsJobs
                    .Where(j => j.CreatedAt >= monthStart && j.CreatedAt < monthEnd)
                    .ToList();
                return new JobThroughputDto(
                    $"{x.Year}-{x.Month:D2}",
                    monthJobs.Count(j => j.Status == JobStatus.Completed),
                    monthJobs.Count(j => j.Status == JobStatus.Failed),
                    monthJobs.Count(j => j.Status == JobStatus.Queued || j.Status == JobStatus.Running)
                );
            })
            .ToList();

        IReadOnlyList<SearchQueryVolumeDto> searchQueryVolume;
        var hasSavedSearchCreatedAt = dbContext.SavedSearches.AsNoTracking().Any();
        if (hasSavedSearchCreatedAt)
        {
            var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30);
            var searches = await dbContext.SavedSearches.AsNoTracking()
                .Where(s => s.CreatedAt >= thirtyDaysAgo)
                .GroupBy(s => s.CreatedAt.Date)
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderBy(x => x.Key)
                .ToListAsync(cancellationToken);

            searchQueryVolume = searches
                .Select(x => new SearchQueryVolumeDto(x.Key.ToString("yyyy-MM-dd"), x.Count))
                .ToList();
        }
        else
        {
            searchQueryVolume = [];
        }

        var completedJobs = await jobs
            .Where(j => j.Status == JobStatus.Completed && j.UpdatedAt >= twelveMonthsAgo)
            .ToListAsync(cancellationToken);

        long avgProcessingTime = 0;
        if (completedJobs.Count > 0)
        {
            var processingTimes = new List<long>();
            foreach (var job in completedJobs)
            {
                if (!string.IsNullOrEmpty(job.ResultJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(job.ResultJson);
                        if (doc.RootElement.TryGetProperty("processingTimeMs", out var prop) && prop.ValueKind == JsonValueKind.Number)
                        {
                            processingTimes.Add(prop.GetInt64());
                        }
                    }
                    catch
                    {
                    }
                }
            }
            if (processingTimes.Count > 0)
            {
                avgProcessingTime = (long)processingTimes.Average();
            }
        }

        var topKeywords = await GetTopKeywordsAsync(papers, cancellationToken);

        return new AnalyticsDto(
            totalPapers,
            papersOverTimeDtos,
            papersBySource,
            papersByStatus,
            papersByVenue,
            papersByYear,
            jobThroughput,
            searchQueryVolume,
            avgProcessingTime,
            topKeywords
        );
    }

    private async Task<IReadOnlyList<TopKeywordDto>> GetTopKeywordsAsync(
        IQueryable<Paper> papers,
        CancellationToken cancellationToken)
    {
        var texts = await papers
            .Where(p => !string.IsNullOrEmpty(p.Title) || !string.IsNullOrEmpty(p.Abstract))
            .Select(p => (p.Title ?? "") + " " + (p.Abstract ?? ""))
            .ToListAsync(cancellationToken);

        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var wordPattern = new Regex(@"\b[a-zA-Z]{3,}\b");

        foreach (var text in texts)
        {
            var words = wordPattern.Matches(text);
            foreach (Match match in words)
            {
                var word = match.Value.ToLowerInvariant();
                if (!StopWords.Contains(word) && !char.IsDigit(word[0]))
                {
                    if (wordCounts.TryGetValue(word, out var count))
                        wordCounts[word] = count + 1;
                    else
                        wordCounts[word] = 1;
                }
            }
        }

        return wordCounts
            .OrderByDescending(x => x.Value)
            .Take(20)
            .Select(x => new TopKeywordDto(x.Key, x.Value))
            .ToList();
    }
}
