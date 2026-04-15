using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Analysis;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class AnalysisService(
    ApplicationDbContext dbContext,
    IJobService jobService,
    OpenRouterChatClient openRouterChatClient,
    ISemanticScholarClient semanticScholarClient,
    ILogger<AnalysisService> logger) : IAnalysisService
{
    private const string ComparisonJsonSchema = """
        Return valid JSON only.
        Schema:
        overlapSummary: string
        contradictionHints: string[]
        noveltyHints: string[]
        commonThemes: string[]
        fieldBridgingNotes: string
        scoringMetadata: object
        """;

    public async Task<AnalysisResultModel> ComparePapersAsync(ComparePapersCommand command, CancellationToken cancellationToken)
    {
        var papers = await dbContext.Papers
            .AsNoTracking()
            .Include(p => p.Documents)
            .Include(p => p.Summaries)
            .Where(p => p.Id == command.LeftPaperId || p.Id == command.RightPaperId)
            .ToListAsync(cancellationToken);

        var left = papers.FirstOrDefault(p => p.Id == command.LeftPaperId) ?? throw new NotFoundException(nameof(Paper), command.LeftPaperId);
        var right = papers.FirstOrDefault(p => p.Id == command.RightPaperId) ?? throw new NotFoundException(nameof(Paper), command.RightPaperId);

        var systemPrompt = $"You are an expert scientific comparison analyst.\n{ComparisonJsonSchema}";

        var userPrompt = $"""
Compare these two papers and focus on transferable ideas, contradictions, and cross-industry insights.

LEFT PAPER
{QueryHelpers.FormatPaper(left)}

RIGHT PAPER
{QueryHelpers.FormatPaper(right)}
""";

        var resultNode = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

        var entity = new AnalysisResult
        {
            AnalysisType = AnalysisType.ComparePapers,
            InputSetJson = new JsonObject
            {
                ["leftPaperId"] = left.Id,
                ["rightPaperId"] = right.Id
            }.ToJsonString(),
            ResultJson = resultNode?.ToJsonString(),
            CreatedBy = command.RequestedBy
        };

        dbContext.AnalysisResults.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created compare-papers analysis result {AnalysisResultId}", entity.Id);
        return entity.ToModel();
    }

    public async Task<AnalysisResultModel> CompareFieldsAsync(CompareFieldsCommand command, CancellationToken cancellationToken)
    {
        var leftPapers = await QueryPapersForFilter(command.LeftFilter, cancellationToken);
        var rightPapers = await QueryPapersForFilter(command.RightFilter, cancellationToken);

        var systemPrompt = $"You are an expert scientific field-comparison analyst.\n{ComparisonJsonSchema}";

        var userPrompt = $"""
Compare these two research clusters.

LEFT FILTER: {command.LeftFilter}
{QueryHelpers.FormatPapers(leftPapers)}

RIGHT FILTER: {command.RightFilter}
{QueryHelpers.FormatPapers(rightPapers)}
""";

        var resultNode = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

        var entity = new AnalysisResult
        {
            AnalysisType = AnalysisType.CompareFields,
            InputSetJson = new JsonObject
            {
                ["leftFilter"] = command.LeftFilter,
                ["rightFilter"] = command.RightFilter
            }.ToJsonString(),
            ResultJson = resultNode?.ToJsonString(),
            CreatedBy = command.RequestedBy
        };

        dbContext.AnalysisResults.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created compare-fields analysis result {AnalysisResultId}", entity.Id);
        return entity.ToModel();
    }

    public async Task<AnalysisJobStatusModel> GenerateInsightsAsync(GenerateInsightsCommand command, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["filter"] = command.Filter
        };

        var job = await jobService.CreateAsync(
            new CreateJobCommand(JobType.Analysis, payload, null, command.RequestedBy),
            cancellationToken);

        var analysisResult = new AnalysisResult
        {
            JobId = job.Id,
            AnalysisType = AnalysisType.GenerateInsights,
            InputSetJson = new JsonObject
            {
                ["filter"] = command.Filter
            }.ToJsonString(),
            ResultJson = null,
            CreatedBy = command.RequestedBy
        };

        dbContext.AnalysisResults.Add(analysisResult);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created generate-insights analysis job {JobId}", job.Id);
        return new AnalysisJobStatusModel(job.Id, job.Status, job.ErrorMessage, analysisResult.ToModel());
    }

    public async Task<AnalysisJobStatusModel> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), jobId);

        var result = await dbContext.AnalysisResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.JobId == jobId, cancellationToken);

        return new AnalysisJobStatusModel(job.Id, job.Status, job.ErrorMessage, result?.ToModel());
    }

    public async Task<IReadOnlyList<AnalysisResultModel>> GetAllAsync(CancellationToken cancellationToken)
    {
        var results = await dbContext.AnalysisResults
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return results.Select(r => r.ToModel()).ToList();
    }

    public async Task DeleteAsync(Guid analysisResultId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.AnalysisResults.FirstOrDefaultAsync(r => r.Id == analysisResultId, cancellationToken)
            ?? throw new NotFoundException(nameof(AnalysisResult), analysisResultId);

        dbContext.AnalysisResults.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted analysis result {AnalysisResultId}", analysisResultId);
    }

    public async Task<ResearchGapReportModel> IdentifyResearchGapAsync(IdentifyResearchGapCommand command, CancellationToken cancellationToken)
    {
        var corpusPapers = await dbContext.Papers
            .AsNoTracking()
            .Where(p => EF.Functions.ILike(p.Title, $"%{EscapeILikePattern(command.Topic)}%") || EF.Functions.ILike(p.Abstract ?? string.Empty, $"%{EscapeILikePattern(command.Topic)}%"))
            .Take(50)
            .ToListAsync(cancellationToken);

        var corpusCoverage = new JsonObject
        {
            ["topic"] = command.Topic,
            ["papersFound"] = corpusPapers.Count,
            ["papers"] = new JsonArray(corpusPapers.Select(p => new JsonObject
            {
                ["id"] = p.Id.ToString(),
                ["title"] = p.Title,
                ["year"] = p.Year ?? 0,
                ["venue"] = p.Venue ?? ""
            }).ToArray())
        };

        var externalPapers = await semanticScholarClient.SearchPapersAsync([command.Topic], 20, cancellationToken);
        var externalCoverage = new JsonObject
        {
            ["topic"] = command.Topic,
            ["papersFound"] = externalPapers.Count,
            ["papers"] = new JsonArray(externalPapers.Select(p => new JsonObject
            {
                ["externalId"] = p.SemanticScholarId,
                ["title"] = p.Title,
                ["year"] = p.Year ?? 0,
                ["venue"] = p.Venue ?? "",
                ["citationCount"] = p.CitationCount
            }).ToArray())
        };

        var systemPrompt = """
You are an expert research gap analyst. Return valid JSON only.
Schema:
understudiedAngles: string[]
researchOpportunities: string[]
suggestedQueries: string[]
coverageGaps: object
comparisonSummary: string
""";

        var userPrompt = $"""
Analyze research gaps for topic: {command.Topic}

CORPUS COVERAGE (papers in our system):
{corpusCoverage.ToJsonString()}

EXTERNAL COVERAGE (papers from Semantic Scholar):
{externalCoverage.ToJsonString()}

Identify:
1. What angles are understudied in our corpus vs external literature
2. Research opportunities
3. Suggested follow-up queries
4. Coverage gaps
""";

        var llmResult = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

        var gapAnalysisJson = llmResult?["understudiedAngles"]?.ToJsonString() ?? "[]";
        var suggestedQueriesJson = llmResult?["suggestedQueries"]?.ToJsonString() ?? "[]";

        var entity = new ResearchGap
        {
            Topic = command.Topic,
            GapAnalysisJson = gapAnalysisJson,
            CorpusCoverageJson = corpusCoverage.ToJsonString(),
            ExternalCoverageJson = externalCoverage.ToJsonString(),
            SuggestedQueriesJson = suggestedQueriesJson,
            CreatedBy = command.RequestedBy
        };

        dbContext.ResearchGaps.Add(entity);

        var analysisResult = new AnalysisResult
        {
            AnalysisType = AnalysisType.ResearchGap,
            InputSetJson = new JsonObject { ["topic"] = command.Topic }.ToJsonString(),
            ResultJson = llmResult?.ToJsonString(),
            CreatedBy = command.RequestedBy
        };
        dbContext.AnalysisResults.Add(analysisResult);

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created research gap analysis for topic {Topic}", command.Topic);

        return new ResearchGapReportModel(
            entity.Id,
            entity.Topic,
            entity.GapAnalysisJson is not null ? JsonNode.Parse(entity.GapAnalysisJson) : null,
            entity.CorpusCoverageJson is not null ? JsonNode.Parse(entity.CorpusCoverageJson) : null,
            entity.ExternalCoverageJson is not null ? JsonNode.Parse(entity.ExternalCoverageJson) : null,
            entity.SuggestedQueriesJson is not null ? JsonNode.Parse(entity.SuggestedQueriesJson) : null,
            entity.CreatedBy,
            entity.CreatedAt);
    }

    private Task<List<Paper>> QueryPapersForFilter(string filter, CancellationToken cancellationToken) =>
        QueryHelpers.QueryPapersForFilterAsync(dbContext.Papers, filter, 10, cancellationToken);

    private static string EscapeILikePattern(string pattern)
    {
        return pattern.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }
}