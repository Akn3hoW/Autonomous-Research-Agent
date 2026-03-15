using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Analysis;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
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

    private Task<List<Paper>> QueryPapersForFilter(string filter, CancellationToken cancellationToken) =>
        QueryHelpers.QueryPapersForFilterAsync(dbContext.Papers, filter, 10, cancellationToken);
}
