using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Analysis;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class AnalysisService(
    ApplicationDbContext dbContext,
    IJobService jobService,
    ILogger<AnalysisService> logger) : IAnalysisService
{
    public async Task<AnalysisResultModel> ComparePapersAsync(ComparePapersCommand command, CancellationToken cancellationToken)
    {
        var papersById = await dbContext.Papers
            .AsNoTracking()
            .Where(p => p.Id == command.LeftPaperId || p.Id == command.RightPaperId)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        if (!papersById.TryGetValue(command.LeftPaperId, out var left))
            throw new NotFoundException(nameof(Paper), command.LeftPaperId);
        if (!papersById.TryGetValue(command.RightPaperId, out var right))
            throw new NotFoundException(nameof(Paper), command.RightPaperId);

        var commonTheme = InferCommonTheme(left.Title, right.Title);

        var resultNode = new JsonObject
        {
            ["overlapSummary"] = $"Both papers address adjacent themes around '{commonTheme}'.",
            ["contradictionHints"] = new JsonArray(),
            ["noveltyHints"] = new JsonArray(JsonValue.Create($"Citation delta: {Math.Abs(left.CitationCount - right.CitationCount)}")),
            ["commonThemes"] = new JsonArray(JsonValue.Create(commonTheme)),
            ["fieldBridgingNotes"] = $"{left.Venue ?? "Unknown venue"} vs {right.Venue ?? "Unknown venue"} suggests cross-field review potential.",
            ["scoringMetadata"] = new JsonObject
            {
                ["yearDifference"] = Math.Abs((left.Year ?? 0) - (right.Year ?? 0)),
                ["citationDifference"] = Math.Abs(left.CitationCount - right.CitationCount)
            }
        };

        var entity = new AnalysisResult
        {
            AnalysisType = AnalysisType.ComparePapers,
            InputSetJson = new JsonObject
            {
                ["leftPaperId"] = left.Id,
                ["rightPaperId"] = right.Id
            }.ToJsonString(),
            ResultJson = resultNode.ToJsonString(),
            CreatedBy = command.RequestedBy
        };

        dbContext.AnalysisResults.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created compare-papers analysis result {AnalysisResultId}", entity.Id);
        return entity.ToModel();
    }

    public async Task<AnalysisResultModel> CompareFieldsAsync(CompareFieldsCommand command, CancellationToken cancellationToken)
    {
        var leftCount = await CountPapersForFilterAsync(command.LeftFilter, cancellationToken);
        var rightCount = await CountPapersForFilterAsync(command.RightFilter, cancellationToken);

        var resultNode = new JsonObject
        {
            ["overlapSummary"] = $"Compared two filtered sets: '{command.LeftFilter}' vs '{command.RightFilter}'.",
            ["contradictionHints"] = new JsonArray(),
            ["noveltyHints"] = new JsonArray(JsonValue.Create("Use this baseline output as a handoff point for richer analysis pipelines.")),
            ["commonThemes"] = new JsonArray(),
            ["fieldBridgingNotes"] = "Future versions can enrich this with cross-field clustering and citation graph analysis.",
            ["scoringMetadata"] = new JsonObject
            {
                ["leftCount"] = leftCount,
                ["rightCount"] = rightCount
            }
        };

        var entity = new AnalysisResult
        {
            AnalysisType = AnalysisType.CompareFields,
            InputSetJson = new JsonObject
            {
                ["leftFilter"] = command.LeftFilter,
                ["rightFilter"] = command.RightFilter
            }.ToJsonString(),
            ResultJson = resultNode.ToJsonString(),
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
            new Application.Jobs.CreateJobCommand(JobType.Analysis, payload, null, command.RequestedBy),
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

    private async Task<int> CountPapersForFilterAsync(string filter, CancellationToken cancellationToken)
    {
        var pattern = QueryHelpers.ToILikePattern(filter);
        return await dbContext.Papers.CountAsync(
            p => EF.Functions.ILike(p.Title, pattern) || (p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern)),
            cancellationToken);
    }

    private static string InferCommonTheme(string leftTitle, string rightTitle)
    {
        var leftWords = leftTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rightWords = rightTitle.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return leftWords.Intersect(rightWords, StringComparer.OrdinalIgnoreCase).FirstOrDefault() ?? "related research areas";
    }
}
