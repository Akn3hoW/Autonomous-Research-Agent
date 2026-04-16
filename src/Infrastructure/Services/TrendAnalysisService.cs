using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Trends;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class TrendAnalysisService(
    ApplicationDbContext dbContext,
    IJobService jobService,
    IOpenRouterChatClient openRouterChatClient,
    ILogger<TrendAnalysisService> logger) : ITrendAnalysisService
{
    private const string TopicExtractionSchema = """
        Return valid JSON only.
        Schema:
        topics: array of objects with topic (string), paperCount (number), sampleTitles (array of strings)
        """;

    public async Task<TrendsResponse> GetTrendsAsync(TrendsRequest request, CancellationToken cancellationToken)
    {
        var endYear = request.EndYear;
        var startYear = request.StartYear;

        if (endYear == 0) endYear = DateTime.UtcNow.Year;
        if (startYear == 0) startYear = endYear - 9;

        var papersQuery = dbContext.Papers
            .AsNoTracking()
            .Where(p => p.Year.HasValue && p.Year >= startYear && p.Year <= endYear);

        if (!string.IsNullOrWhiteSpace(request.Field))
        {
            var pattern = $"%{request.Field}%";
            papersQuery = papersQuery.Where(p =>
                EF.Functions.ILike(p.Title, pattern) ||
                (p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern)));
        }

        var papers = await papersQuery
            .OrderBy(p => p.Year)
            .ToListAsync(cancellationToken);

        var buckets = new List<TrendBucket>();
        var yearlyPapers = papers.GroupBy(p => p.Year!.Value).OrderBy(g => g.Key);

        var previousYearTopics = new Dictionary<string, int>();
        var allTopicsMomentum = new Dictionary<string, List<int>>();

        foreach (var yearGroup in yearlyPapers)
        {
            var year = yearGroup.Key;
            var yearPapers = yearGroup.ToList();

            var titlesAndAbstracts = yearPapers
                .Select(p => $"Title: {p.Title}\nAbstract: {p.Abstract ?? "N/A"}")
                .Take(50)
                .ToList();

            var topicsResult = await ExtractTopicsAsync(year, titlesAndAbstracts, cancellationToken);

            var currentYearTopics = new Dictionary<string, (int Count, List<string> Samples)>();
            var topicsArray = topicsResult?["topics"]?.AsArray() ?? [];
            foreach (var topicNode in topicsArray)
            {
                var topicName = topicNode?["topic"]?.GetValue<string>() ?? "Unknown";
                var count = topicNode?["paperCount"]?.GetValue<int>() ?? 0;
                var samples = topicNode?["sampleTitles"]?.AsArray()
                    .Select(s => s?.GetValue<string>() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Take(3)
                    .ToList() ?? [];

                currentYearTopics[topicName] = (count, samples);

                if (!allTopicsMomentum.ContainsKey(topicName))
                    allTopicsMomentum[topicName] = [];

                while (allTopicsMomentum[topicName].Count < year - startYear)
                    allTopicsMomentum[topicName].Add(0);

                allTopicsMomentum[topicName].Add(count);
            }

            var topics = currentYearTopics.Select(kv => new TrendTopic(
                kv.Key,
                CalculateMomentum(kv.Value.Count, previousYearTopics.GetValueOrDefault(kv.Key, 0)),
                kv.Value.Count,
                kv.Value.Samples
            )).ToList();

            buckets.Add(new TrendBucket(year, topics));
            previousYearTopics = currentYearTopics.ToDictionary(k => k.Key, k => k.Value.Count);
        }

        var emergingThemes = allTopicsMomentum
            .Where(kv => kv.Value.Count >= 2 && kv.Value[^1] > kv.Value[^2])
            .OrderByDescending(kv => kv.Value[^1] - kv.Value[^2])
            .Take(5)
            .Select(kv => kv.Key)
            .ToList();

        var decliningThemes = allTopicsMomentum
            .Where(kv => kv.Value.Count >= 2 && kv.Value[^1] < kv.Value[^2])
            .OrderByDescending(kv => kv.Value[^2] - kv.Value[^1])
            .Take(5)
            .Select(kv => kv.Key)
            .ToList();

        return new TrendsResponse(buckets, emergingThemes, decliningThemes);
    }

    public async Task<Guid> StartTrendAnalysisJobAsync(string? field, int startYear, int endYear, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["field"] = field ?? "",
            ["startYear"] = startYear,
            ["endYear"] = endYear
        };

        var job = await jobService.CreateAsync(
            new CreateJobCommand(JobType.TrendAnalysis, payload, null, "system"),
            cancellationToken);

        logger.LogInformation("Created TrendAnalysis job {JobId}", job.Id);
        return job.Id;
    }

    private async Task<JsonNode?> ExtractTopicsAsync(int year, List<string> paperTexts, CancellationToken cancellationToken)
    {
        var systemPrompt = $"You are an expert research trend analyst.\n{TopicExtractionSchema}";

        var userPrompt = $"""
Extract the top research topics from papers published in {year}.
For each topic, estimate how many papers relate to it and give 2-3 example paper titles.

Papers:
{string.Join("\n\n---\n\n", paperTexts)}
""";

        return await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);
    }

    private static double CalculateMomentum(int currentCount, int previousCount)
    {
        if (previousCount == 0)
            return currentCount > 0 ? 1.0 : 0.0;

        return (currentCount - previousCount) / (double)previousCount;
    }
}