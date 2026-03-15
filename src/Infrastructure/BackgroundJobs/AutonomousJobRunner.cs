using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.BackgroundJobs;

public sealed class AutonomousJobRunner(
    ApplicationDbContext dbContext,
    IPaperService paperService,
    ISummaryService summaryService,
    ISummarizationService summarizationService,
    PaperDocumentProcessingService paperDocumentProcessingService,
    OpenRouterChatClient openRouterChatClient,
    IOptions<OpenRouterOptions> options,
    ILogger<AutonomousJobRunner> logger) : IJobRunner
{
    private readonly OpenRouterOptions _options = options.Value;

    public async Task RunAsync(Job job, CancellationToken cancellationToken)
    {
        switch (job.Type)
        {
            case JobType.ImportPapers:
                await RunImportPapersAsync(job, cancellationToken);
                break;
            case JobType.SummarizePaper:
                await RunSummarizePaperAsync(job, cancellationToken);
                break;
            case JobType.Analysis:
                await RunGenerateInsightsAsync(job, cancellationToken);
                break;
            case JobType.ProcessPaperDocument:
                await RunProcessPaperDocumentAsync(job, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported job type '{job.Type}'.");
        }

        logger.LogInformation("Completed job {JobId} ({JobType})", job.Id, job.Type);
    }

    private async Task RunImportPapersAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var queries = payload["queries"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
        var limit = payload["limit"]?.GetValue<int>() ?? 10;
        var storeImportedPapers = payload["storeImportedPapers"]?.GetValue<bool>() ?? true;

        var result = await paperService.ImportAsync(new ImportPapersCommand(queries, limit, storeImportedPapers), cancellationToken);
        job.ResultJson = JsonSerializer.SerializeToNode(new
        {
            importedCount = result.ImportedCount,
            paperIds = result.Papers.Select(p => p.Id).ToList()
        })?.ToJsonString();
    }

    private async Task RunSummarizePaperAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var paperId = payload["paperId"]?.GetValue<Guid>() ?? throw new InvalidOperationException("paperId is required.");
        var requestedModelName = payload["modelName"]?.GetValue<string>() ?? _options.Model;
        var promptVersion = payload["promptVersion"]?.GetValue<string>() ?? "v1";

        var paper = await paperService.GetByIdAsync(paperId, cancellationToken);
        var summary = await summarizationService.GenerateSummaryAsync(paper, requestedModelName, promptVersion, cancellationToken);

        var keyFindings = summary?["keyFindings"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty) ?? [];
        var searchText = string.Join(" ", new[]
        {
            summary?["shortSummary"]?.GetValue<string>(),
            summary?["longSummary"]?.GetValue<string>(),
            string.Join(" ", keyFindings)
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        var created = await summaryService.CreateAsync(
            new CreateSummaryCommand(paperId, _options.Model, promptVersion, SummaryStatus.Generated, summary, searchText),
            cancellationToken);

        job.ResultJson = JsonSerializer.SerializeToNode(new { summaryId = created.Id, paperId = created.PaperId, modelName = created.ModelName })?.ToJsonString();
    }

    private async Task RunGenerateInsightsAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var filter = payload["filter"]?.GetValue<string>() ?? string.Empty;

        var papers = string.IsNullOrWhiteSpace(filter)
            ? await dbContext.Papers.AsNoTracking().Include(p => p.Documents).Include(p => p.Summaries)
                .OrderByDescending(p => p.UpdatedAt).Take(12).ToListAsync(cancellationToken)
            : await QueryHelpers.QueryPapersForFilterAsync(dbContext.Papers, filter, 12, cancellationToken);

        var systemPrompt = """
You are an expert scientific research analyst.
Return valid JSON only.
Produce a concise, evidence-grounded cross-paper synthesis.
Schema:
overview: string
emergingThemes: string[]
crossIndustrySignals: string[]
hypotheses: array of objects with hypothesis, rationale, supportingEvidence
suggestedExperiments: array of objects with experiment, objective, expectedSignal
risksAndUnknowns: string[]
confidence: number
""";

        var userPrompt = BuildInsightsPrompt(filter, papers);
        var result = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

        var analysis = await dbContext.AnalysisResults.FirstOrDefaultAsync(r => r.JobId == job.Id, cancellationToken);
        if (analysis is not null)
        {
            analysis.ResultJson = result?.ToJsonString();
        }

        job.ResultJson = result?.ToJsonString();
    }

    private async Task RunProcessPaperDocumentAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var documentId = payload["documentId"]?.GetValue<Guid>() ?? throw new InvalidOperationException("documentId is required.");

        var document = await paperDocumentProcessingService.ProcessAsync(documentId, cancellationToken);
        job.ResultJson = JsonSerializer.SerializeToNode(new
        {
            documentId = document.Id,
            status = document.Status.ToString(),
            storagePath = document.StoragePath,
            extractedAt = document.ExtractedAt
        })?.ToJsonString();
    }

    private static string BuildInsightsPrompt(string filter, IReadOnlyCollection<Domain.Entities.Paper> papers)
    {
        var lines = new List<string>
        {
            $"Filter: {filter}",
            $"Papers considered: {papers.Count}"
        };

        foreach (var paper in papers)
        {
            lines.Add(QueryHelpers.FormatPaper(paper));
        }

        return string.Join("\n\n", lines);
    }

    private static JsonObject ParsePayload(Job job) =>
        JsonNode.Parse(job.PayloadJson)?.AsObject()
        ?? throw new InvalidOperationException($"Job {job.Id} has an invalid payload.");
}
