using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Duplicates;
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

/// <summary>
/// Executes durable background jobs of various types including paper import, summarization, analysis, and research goals.
/// </summary>
/// <remarks>
/// <para>This runner implements the <see cref="IJobRunner"/> interface and processes jobs from the database.
/// It is typically invoked by <see cref="DatabaseJobWorker"/> or other job scheduling mechanisms.</para>
/// <para>Supported job types:</para>
/// <list type="bullet">
///   <item><description>ImportPapers - Bulk import papers from Semantic Scholar using search queries</description></item>
///   <item><description>SummarizePaper - Generate AI summaries for a single paper</description></item>
///   <item><description>ProcessPaperDocument - Download, extract text, and chunk a paper document</description></item>
///   <item><description>Analysis - Generate cross-paper insights using LLM analysis</description></item>
///   <item><description>TrendAnalysis - Analyze research trends over time</description></item>
///   <item><description>ResearchGoal - Orchestrate multi-step research workflows with child jobs</description></item>
///   <item><description>SearchPapers - Search Semantic Scholar for papers</description></item>
///   <item><description>ImportPaper - Import a single paper from search results</description></item>
///   <item><description>SummarizePaperChild - Summarize papers from a parent import job</description></item>
///   <item><description>AnalyzePaper - Generate paper-level analysis</description></item>
///   <item><description>GenerateReport - Generate comprehensive research reports</description></item>
///   <item><description>DuplicateDetection - Detect potential duplicate papers</description></item>
///   <item><description>WatchlistPolling - Check saved searches for new papers</description></item>
/// </list>
/// <para>Error handling:</para>
/// <list type="bullet">
///   <item><description>Job failures are logged and persisted to the job's ErrorMessage field</description></item>
///   <item><description>Partial failures in child job workflows are tracked in ResultJson</description></item>
///   <item><description>Payload validation errors throw InvalidOperationException with descriptive messages</description></item>
/// </list>
/// <para>SLA/Performance characteristics:</para>
/// <list type="bullet">
///   <item><description>Small import jobs (5 papers): ~10-30 seconds</description></item>
///   <item><description>Summarization jobs: ~5-15 seconds per paper (depends on LLM latency)</description></item>
///   <item><description>Document processing jobs: ~1-5 minutes (includes download, extraction, embedding)</description></item>
///   <item><description>Research goal workflows: Variable, depends on child job complexity</description></item>
/// </list>
/// </remarks>
public sealed class AutonomousJobRunner(
    ApplicationDbContext dbContext,
    IPaperService paperService,
    ISummaryService summaryService,
    ISummarizationService summarizationService,
    PaperDocumentProcessingService paperDocumentProcessingService,
    ITextChunkingService textChunkingService,
    IEmbeddingIndexingService embeddingIndexingService,
    IDuplicateDetectionService duplicateDetectionService,
    IJobService jobService,
    OpenRouterChatClient openRouterChatClient,
    ISemanticScholarClient semanticScholarClient,
    IOptions<OpenRouterOptions> options,
    IOptions<SummaryOptions> summaryOptions,
    ILoggerFactory loggerFactory) : IJobRunner
{
    private readonly OpenRouterOptions _options = options.Value;
    private readonly SummaryOptions _summaryOptions = summaryOptions.Value;
    private readonly ILogger<AutonomousJobRunner> _logger = loggerFactory.CreateLogger<AutonomousJobRunner>();
    private readonly ILogger<TrendAnalysisService> _trendAnalysisLogger = loggerFactory.CreateLogger<TrendAnalysisService>();
    private static readonly ActivitySource ActivitySource = new("AutonomousJobRunner");

    /// <summary>
    /// Executes a job based on its type, handling all supported job categories.
    /// </summary>
    /// <param name="job">The job to execute. Must have a valid Type and PayloadJson.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown when payload is malformed or job type is unsupported.</exception>
    public async Task RunAsync(Job job, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("JobExecution", ActivityKind.Internal);
        activity?.SetTag("job.id", job.Id.ToString());
        activity?.SetTag("job.type", job.Type.ToString());

        try
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
                case JobType.TrendAnalysis:
                    await RunTrendAnalysisAsync(job, cancellationToken);
                    break;
                case JobType.ResearchGoal:
                    await RunResearchGoalAsync(job, cancellationToken);
                    break;
                case JobType.SearchPapers:
                    await RunSearchPapersAsync(job, cancellationToken);
                    break;
                case JobType.ImportPaper:
                    await RunImportPaperAsync(job, cancellationToken);
                    break;
                case JobType.SummarizePaperChild:
                    await RunSummarizePaperChildAsync(job, cancellationToken);
                    break;
                case JobType.AnalyzePaper:
                    await RunAnalyzePaperAsync(job, cancellationToken);
                    break;
                case JobType.GenerateReport:
                    await RunGenerateReportAsync(job, cancellationToken);
                    break;
                case JobType.DuplicateDetection:
                    await RunDuplicateDetectionAsync(job, cancellationToken);
                    break;
                case JobType.WatchlistPolling:
                    await RunWatchlistPollingAsync(job, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported job type '{job.Type}'.");
            }

            _logger.LogInformation("Completed job {JobId} ({JobType})", job.Id, job.Type);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task RunImportPapersAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var queriesArray = payload["queries"]?.AsArray();
        var queries = queriesArray != null
            ? queriesArray.Select(x => x?.GetValue<string>() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
            : [];
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

        using (_logger.BeginScope(new KeyValuePair<string, object?>[]
        {
            new("JobId", job.Id),
            new("PaperId", paperId)
        }))
        {
            var requestedModelName = payload["modelName"]?.GetValue<string>() ?? _options.Model;
            var promptVersion = payload["promptVersion"]?.GetValue<string>() ?? _summaryOptions.DefaultPromptVersion;
            var abTestSessionId = payload["abTestSessionId"]?.GetValue<Guid>();

            var paper = await paperService.GetByIdAsync(paperId, null, cancellationToken);
            var summary = await summarizationService.GenerateSummaryAsync(paper, requestedModelName, promptVersion, cancellationToken);

            var keyFindings = summary?["keyFindings"]?.AsArray().Select(x => x?.GetValue<string>()).ToList()
                ?? throw new InvalidOperationException("Missing required field: keyFindings");

            var shortSummary = summary?["shortSummary"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Missing required field: shortSummary");
            var longSummary = summary?["longSummary"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Missing required field: longSummary");

            var searchText = string.Join(" ", new[]
            {
                shortSummary,
                longSummary,
                string.Join(" ", keyFindings.Where(x => !string.IsNullOrWhiteSpace(x)))
            }.Where(x => !string.IsNullOrWhiteSpace(x)));

            var created = await summaryService.CreateAsync(
                new CreateSummaryCommand(paperId, requestedModelName, promptVersion, SummaryStatus.Generated, summary, searchText, abTestSessionId),
                cancellationToken);

            job.ResultJson = JsonSerializer.SerializeToNode(new { summaryId = created.Id, paperId = created.PaperId, modelName = created.ModelName })?.ToJsonString();

            if (abTestSessionId.HasValue)
            {
                await CheckAbTestSessionCompletionAsync(abTestSessionId.Value, cancellationToken);
            }
        }
    }

    private async Task CheckAbTestSessionCompletionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.AbTestSessions
            .Include(s => s.PaperSummaries)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            return;
        }

        var allCompleted = session.PaperSummaries.All(s => s.Status == SummaryStatus.Generated || s.Status == SummaryStatus.Approved || s.Status == SummaryStatus.Rejected);
        var anyFailed = session.PaperSummaries.Any(s => s.Status == SummaryStatus.Rejected);

        if (allCompleted)
        {
            session.Status = anyFailed ? AbTestSessionStatus.Failed : AbTestSessionStatus.Completed;
            session.CompletedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
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
        if (analysis is null)
        {
            analysis = new AnalysisResult
            {
                JobId = job.Id,
                AnalysisType = AnalysisType.GenerateInsights,
                InputSetJson = new JsonObject { ["filter"] = filter }.ToJsonString()
            };
            dbContext.AnalysisResults.Add(analysis);
        }

        analysis.ResultJson = result?.ToJsonString();
        await dbContext.SaveChangesAsync(cancellationToken);

        job.ResultJson = result?.ToJsonString();
    }

    private async Task RunProcessPaperDocumentAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var documentId = payload["documentId"]?.GetValue<Guid>() ?? throw new InvalidOperationException("documentId is required.");

        var document = await paperDocumentProcessingService.ProcessAsync(documentId, cancellationToken);

        if (document.Status == PaperDocumentStatus.Extracted && !string.IsNullOrWhiteSpace(document.ExtractedText))
        {
            await ProcessDocumentChunksAsync(document, cancellationToken);
        }

        job.ResultJson = JsonSerializer.SerializeToNode(new
        {
            documentId = document.Id,
            status = document.Status.ToString(),
            storagePath = document.StoragePath,
            extractedAt = document.ExtractedAt
        })?.ToJsonString();
    }

    private async Task ProcessDocumentChunksAsync(Domain.Entities.PaperDocument document, CancellationToken cancellationToken)
    {
        var existingChunks = await dbContext.DocumentChunks
            .Where(c => c.PaperDocumentId == document.Id)
            .ToListAsync(cancellationToken);
        dbContext.DocumentChunks.RemoveRange(existingChunks);

        var existingEmbeddings = await dbContext.PaperEmbeddings
            .Where(e => e.DocumentChunkId != null && e.EmbeddingType == EmbeddingType.DocumentChunk &&
                dbContext.DocumentChunks.Any(c => c.Id == e.DocumentChunkId && c.PaperDocumentId == document.Id))
            .ToListAsync(cancellationToken);
        dbContext.PaperEmbeddings.RemoveRange(existingEmbeddings);

        var textChunks = textChunkingService.ChunkText(document.ExtractedText!);
        if (textChunks.Count == 0)
        {
            return;
        }

        var chunks = textChunks.Select(tc => new DocumentChunk
        {
            PaperDocumentId = document.Id,
            ChunkIndex = tc.Index,
            Text = tc.Text,
            TextLength = tc.Text.Length,
            StartPosition = tc.StartPosition,
            EndPosition = tc.EndPosition
        }).ToList();

        dbContext.DocumentChunks.AddRange(chunks);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await embeddingIndexingService.UpsertDocumentChunksAsync(chunks, cancellationToken);
            _logger.LogInformation("Created {ChunkCount} chunks and embeddings for document {DocumentId}", chunks.Count, document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding indexing failed for document {DocumentId}. Chunks exist but embeddings are missing. Error: {ErrorMessage}", document.Id, ex.Message);
            dbContext.DocumentChunks.RemoveRange(chunks);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidStateException($"Embedding indexing failed for document {document.Id}: {ex.Message}");
        }
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

    private async Task RunTrendAnalysisAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var field = payload["field"]?.GetValue<string>();
        var startYear = payload["startYear"]?.GetValue<int>() ?? DateTime.UtcNow.Year - 9;
        var endYear = payload["endYear"]?.GetValue<int>() ?? DateTime.UtcNow.Year;

        var request = new Application.Trends.TrendsRequest(field, startYear, endYear);
        var result = await dbContext.TrendResults.FirstOrDefaultAsync(t =>
            t.Field == (field ?? "") &&
            t.StartYear == startYear &&
            t.EndYear == endYear,
            cancellationToken);

        var response = await new TrendAnalysisService(
            dbContext,
            jobService,
            openRouterChatClient,
            _trendAnalysisLogger).GetTrendsAsync(request, cancellationToken);

        if (result is null)
        {
            result = new TrendResult
            {
                Field = field ?? "",
                StartYear = startYear,
                EndYear = endYear
            };
            dbContext.TrendResults.Add(result);
        }

        result.ResultJson = JsonSerializer.SerializeToNode(response)?.ToJsonString();
        result.CalculatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        job.ResultJson = result.ResultJson;
    }

    private async Task RunResearchGoalAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var goal = payload["goal"]?.GetValue<string>() ?? string.Empty;
        var maxPapers = payload["maxPapers"]?.GetValue<int>() ?? 20;
        var field = payload["field"]?.GetValue<string>();

        job.Status = JobStatus.Running;
        await dbContext.SaveChangesAsync(cancellationToken);

        var childJobs = await dbContext.Jobs
            .Where(j => j.ParentJobId == job.Id)
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var childJob in childJobs)
        {
            childJob.Status = JobStatus.Running;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var parentActivity = Activity.Current?.Context ?? default;
        var completedChildJobs = new List<Job>();

        foreach (var childJob in childJobs)
        {
            using (_logger.BeginScope(new KeyValuePair<string, object?>[]
            {
                new("JobId", childJob.Id),
                new("JobType", childJob.Type)
            }))
            {
                using var childActivity = ActivitySource.StartActivity("JobExecution", ActivityKind.Internal, parentActivity);
                childActivity?.SetTag("job.id", childJob.Id.ToString());
                childActivity?.SetTag("job.type", childJob.Type.ToString());
                try
                {
                    await RunAsync(childJob, cancellationToken);
                    completedChildJobs.Add(childJob);
                }
                catch (Exception ex)
                {
                    childActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    childJob.Status = JobStatus.Failed;
                    childJob.ErrorMessage = ex.Message;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    foreach (var completedChild in completedChildJobs)
                    {
                        completedChild.Status = JobStatus.Superseded;
                        _logger.LogInformation("Marked child job {ChildJobId} as superseded due to sibling failure in parent job {ParentJobId}", completedChild.Id, job.Id);
                    }
                    await dbContext.SaveChangesAsync(cancellationToken);

                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = $"Child job {childJob.Id} failed: {ex.Message}";
                    await dbContext.SaveChangesAsync(cancellationToken);
                    throw;
                }
            }
        }

        job.Status = JobStatus.Completed;
        job.ResultJson = new JsonObject
        {
            ["goal"] = goal,
            ["childJobs"] = new JsonArray(childJobs.Select(c => new JsonObject
            {
                ["id"] = c.Id,
                ["type"] = c.Type.ToString(),
                ["status"] = c.Status.ToString(),
                ["result"] = c.ResultJson is null ? null : JsonNode.Parse(c.ResultJson)
            }).ToArray())
        }?.ToJsonString();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RunSearchPapersAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var goal = payload["goal"]?.GetValue<string>() ?? string.Empty;
        var maxPapers = payload["maxPapers"]?.GetValue<int>() ?? 20;
        var field = payload["field"]?.GetValue<string>();

        var queries = new List<string> { goal };
        if (!string.IsNullOrWhiteSpace(field))
        {
            queries.Add(field);
        }

        var searchResults = await semanticScholarClient.SearchPapersAsync(queries, maxPapers, cancellationToken);

        job.ResultJson = new JsonObject
        {
            ["searchResults"] = new JsonArray(searchResults.Select(r => new JsonObject
            {
                ["semanticScholarId"] = r.SemanticScholarId,
                ["title"] = r.Title,
                ["authors"] = new JsonArray(r.Authors.Select(a => JsonValue.Create(a)).ToArray()),
                ["year"] = r.Year,
                ["venue"] = r.Venue
            }).ToArray()),
            ["count"] = searchResults.Count
        }?.ToJsonString();
    }

    private async Task RunImportPaperAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var searchJobId = payload["searchJobId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(searchJobId) || !Guid.TryParse(searchJobId, out var searchId))
        {
            throw new InvalidOperationException("searchJobId is required.");
        }

        var searchJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == searchId, cancellationToken);
        if (searchJob == null)
        {
            throw new InvalidOperationException($"Search job {searchId} not found.");
        }

        var searchResultNode = JsonNode.Parse(searchJob.ResultJson ?? "{}")?.AsObject();
        var searchResults = searchResultNode?["searchResults"]?.AsArray()
            .Select(x => new SemanticScholarPaperImportModel(
                x?["semanticScholarId"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                x?["doi"]?.GetValue<string>(),
                x?["title"]?.GetValue<string>() ?? "Untitled",
                x?["abstract"]?.GetValue<string>(),
                x?["authors"]?.AsArray().Select(a => a?.GetValue<string>() ?? string.Empty).ToList() ?? [],
                x?["year"]?.GetValue<int>(),
                x?["venue"]?.GetValue<string>(),
                x?["citationCount"]?.GetValue<int>() ?? 0,
                null))
            .ToList() ?? [];

        var importResult = await paperService.ImportAsync(
            new ImportPapersCommand(searchResults.Select(r => r.Title).ToList(), searchResults.Count, true),
            cancellationToken);

        job.ResultJson = new JsonObject
        {
            ["importedCount"] = importResult.ImportedCount,
            ["paperIds"] = new JsonArray(importResult.Papers.Select(p => JsonValue.Create(p.Id)).ToArray())
        }?.ToJsonString();
    }

    private async Task RunSummarizePaperChildAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var importJobId = payload["importJobId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(importJobId) || !Guid.TryParse(importJobId, out var importId))
        {
            throw new InvalidOperationException("importJobId is required.");
        }

        var importJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == importId, cancellationToken);
        if (importJob == null)
        {
            throw new InvalidOperationException($"Import job {importId} not found.");
        }

        var importResultNode = JsonNode.Parse(importJob.ResultJson ?? "{}")?.AsObject();
        var paperIds = importResultNode?["paperIds"]?.AsArray()
            .Select(x => x?.GetValue<Guid>())
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList() ?? [];

        var summaryResults = new List<JsonNode>();
        foreach (var paperId in paperIds)
        {
            try
            {
            var paper = await paperService.GetByIdAsync(paperId, null, cancellationToken);
                var summary = await summarizationService.GenerateSummaryAsync(paper, _options.Model, _summaryOptions.DefaultPromptVersion, cancellationToken);

                await summaryService.CreateAsync(
                    new CreateSummaryCommand(paperId, _options.Model, _summaryOptions.DefaultPromptVersion, SummaryStatus.Generated, summary, null),
                    cancellationToken);

                summaryResults.Add(new JsonObject
                {
                    ["paperId"] = paperId,
                    ["status"] = "success"
                });
            }
            catch (Exception ex)
            {
                summaryResults.Add(new JsonObject
                {
                    ["paperId"] = paperId,
                    ["status"] = "failed",
                    ["error"] = ex.Message
                });
            }
        }

        job.ResultJson = new JsonObject
        {
            ["summaries"] = new JsonArray(summaryResults.ToArray())
        }?.ToJsonString();
    }

    private async Task RunAnalyzePaperAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var summarizeJobId = payload["summarizeJobId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(summarizeJobId) || !Guid.TryParse(summarizeJobId, out var summarizeId))
        {
            throw new InvalidOperationException("summarizeJobId is required.");
        }

        var summarizeJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == summarizeId, cancellationToken);
        if (summarizeJob == null)
        {
            throw new InvalidOperationException($"Summarize job {summarizeId} not found.");
        }

        var papers = await dbContext.Papers
            .AsNoTracking()
            .Include(p => p.Summaries)
            .OrderByDescending(p => p.UpdatedAt)
            .Take(12)
            .ToListAsync(cancellationToken);

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

        var userPrompt = BuildInsightsPrompt(string.Empty, papers);
        var result = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

        job.ResultJson = result?.ToJsonString();
    }

    private async Task RunGenerateReportAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var goal = payload["goal"]?.GetValue<string>() ?? string.Empty;
        var analyzeJobId = payload["analyzeJobId"]?.GetValue<string>();

        JsonNode? analysisResult = null;
        if (!string.IsNullOrWhiteSpace(analyzeJobId) && Guid.TryParse(analyzeJobId, out var analyzeId))
        {
            var analyzeJob = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == analyzeId, cancellationToken);
            analysisResult = JsonNode.Parse(analyzeJob?.ResultJson ?? "{}");
        }

        var systemPrompt = """
You are an expert scientific research report writer.
Return valid JSON only.
Generate a comprehensive research report based on the analysis.
Schema:
title: string
executiveSummary: string
introduction: string
methodology: string
findings: object with keyFindings, themes, signals
discussion: string
limitations: string[]
futureResearch: string[]
conclusions: string
references: array of objects with title, authors, year, venue
""";

        var userPrompt = $"Research Goal: {goal}\n\nAnalysis Results:\n{analysisResult?.ToJsonString() ?? "{}"}";
        var report = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

        job.ResultJson = report?.ToJsonString();
    }

    private async Task RunDuplicateDetectionAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var threshold = payload["threshold"]?.GetValue<double>() ?? 0.95;

        job.Status = JobStatus.Running;
        await dbContext.SaveChangesAsync(cancellationToken);

        await duplicateDetectionService.ComputeDuplicatePairsAsync(threshold, cancellationToken);

        job.Status = JobStatus.Completed;
        job.ResultJson = JsonSerializer.SerializeToNode(new { threshold, completedAt = DateTimeOffset.UtcNow })?.ToJsonString();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RunWatchlistPollingAsync(Job job, CancellationToken cancellationToken)
    {
        var payload = ParsePayload(job);
        var savedSearchId = payload["savedSearchId"]?.GetValue<Guid>();

        if (savedSearchId == null)
        {
            throw new InvalidOperationException("savedSearchId is required for WatchlistPolling job.");
        }

        var savedSearch = await dbContext.SavedSearches.FirstOrDefaultAsync(s => s.Id == savedSearchId, cancellationToken);
        if (savedSearch == null)
        {
            throw new InvalidOperationException($"Saved search {savedSearchId} not found.");
        }

        var queries = new List<string> { savedSearch.Query };
        if (!string.IsNullOrWhiteSpace(savedSearch.Field))
        {
            queries = new List<string> { $"{savedSearch.Query} {savedSearch.Field}" };
        }

        var searchResults = await semanticScholarClient.SearchPapersAsync(queries, 10, cancellationToken);

        var semanticIds = searchResults.Select(c => c.SemanticScholarId).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        var dois = searchResults.Select(c => c.Doi).Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        var existingPapers = await dbContext.Papers
            .Where(p => (p.SemanticScholarId != null && semanticIds.Contains(p.SemanticScholarId))
                     || (p.Doi != null && dois.Contains(p.Doi)))
            .ToListAsync(cancellationToken);

        var existingSemanticIds = existingPapers.Where(p => p.SemanticScholarId != null).Select(p => p.SemanticScholarId!).ToHashSet();
        var existingDois = existingPapers.Where(p => p.Doi != null).Select(p => p.Doi!).ToHashSet();

        var newPapers = searchResults
            .Where(r => !string.IsNullOrWhiteSpace(r.SemanticScholarId) && !existingSemanticIds.Contains(r.SemanticScholarId!) ||
                        !string.IsNullOrWhiteSpace(r.Doi) && !existingDois.Contains(r.Doi!))
            .ToList();

        var newCount = newPapers.Count;

        if (newCount > 0)
        {
            foreach (var candidate in newPapers)
            {
                var paper = new Paper
                {
                    SemanticScholarId = candidate.SemanticScholarId,
                    Doi = candidate.Doi,
                    Title = candidate.Title,
                    Abstract = candidate.Abstract,
                    Authors = candidate.Authors.Where(a => !string.IsNullOrWhiteSpace(a)).ToList(),
                    Year = candidate.Year,
                    Venue = candidate.Venue,
                    CitationCount = candidate.CitationCount,
                    Source = PaperSource.SemanticScholar,
                    Status = PaperStatus.Imported,
                    MetadataJson = JsonSerializer.SerializeToNode(candidate.Metadata)?.ToJsonString()
                };
                dbContext.Papers.Add(paper);
            }

            var notification = new Notification
            {
                UserId = savedSearch.UserId,
                Title = "New papers found",
                Message = $"Found {newCount} new paper(s) matching your saved search: \"{savedSearch.Query}\"",
                LinkUrl = $"/papers?search={Uri.EscapeDataString(savedSearch.Query)}"
            };
            dbContext.Notifications.Add(notification);
        }

        savedSearch.LastRunAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        job.ResultJson = JsonSerializer.SerializeToNode(new
        {
            savedSearchId = savedSearch.Id,
            newPapersCount = newCount,
            totalResultsFound = searchResults.Count,
            ranAt = DateTimeOffset.UtcNow
        })?.ToJsonString();
    }

    private async Task RunGenerateClusterMapAsync(Job job, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Running;
        await dbContext.SaveChangesAsync(cancellationToken);

        var embeddings = await dbContext.PaperEmbeddings
            .AsNoTracking()
            .Where(e => e.PaperId != null && e.Vector != null && (e.EmbeddingType == EmbeddingType.PaperAbstract || e.EmbeddingType == EmbeddingType.PaperSummary))
            .Include(e => e.Paper)
            .ToListAsync(cancellationToken);

        if (embeddings.Count < 2)
        {
            job.Status = JobStatus.Completed;
            job.ResultJson = JsonSerializer.SerializeToNode(new { message = "Not enough embeddings for clustering", count = embeddings.Count })?.ToJsonString();
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var vectors = embeddings
            .Where(e => e.Vector != null)
            .Select(e => e.Vector!)
            .ToList();

        var normalizedVectors = NormalizeVectors(vectors);
        var coords = ComputeUmapCoordinates(normalizedVectors);

        var paperCoords = embeddings
            .Where(e => e.Vector != null)
            .Zip(coords, (e, c) => new { PaperId = e.PaperId, X = c.X, Y = c.Y })
            .Where(x => x.PaperId.HasValue)
            .GroupBy(x => x.PaperId!.Value)
            .Select(g => g.First())
            .ToList();

        foreach (var pc in paperCoords)
        {
            var paper = await dbContext.Papers.FindAsync(new object[] { pc.PaperId!.Value }, cancellationToken);
            if (paper != null)
            {
                paper.ClusterX = pc.X;
                paper.ClusterY = pc.Y;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        job.Status = JobStatus.Completed;
        job.ResultJson = JsonSerializer.SerializeToNode(new
        {
            clusteredPapers = paperCoords.Count,
            totalEmbeddings = embeddings.Count,
            completedAt = DateTimeOffset.UtcNow
        })?.ToJsonString();
    }

    private async Task RunExtractConceptsAsync(Job job, CancellationToken cancellationToken)
    {
        job.Status = JobStatus.Running;
        await dbContext.SaveChangesAsync(cancellationToken);

        var papers = await dbContext.Papers
            .AsNoTracking()
            .Include(p => p.Summaries)
            .Where(p => p.Status == PaperStatus.Ready || p.Summaries.Any())
            .ToListAsync(cancellationToken);

        var existingConcepts = await dbContext.PaperConcepts
            .Where(c => papers.Select(p => p.Id).Contains(c.PaperId))
            .ToListAsync(cancellationToken);
        dbContext.PaperConcepts.RemoveRange(existingConcepts);
        await dbContext.SaveChangesAsync(cancellationToken);

        var systemPrompt = """
You are an expert at extracting structured information from research paper summaries.
Return valid JSON only.
Extract all mentioned research methods, datasets, metrics, and models.
Schema:
concepts: array of objects with type (Method|Dataset|Metric|Model), name (string), confidence (0-1)
Types must be exactly: Method, Dataset, Metric, or Model
""";

        var allConcepts = new List<PaperConcept>();

        foreach (var paper in papers)
        {
            try
            {
                var summaryText = paper.Summaries.FirstOrDefault()?.SearchText ?? paper.Abstract ?? "";
                if (string.IsNullOrWhiteSpace(summaryText))
                    continue;

                var userPrompt = $"Paper Title: {paper.Title}\n\nAbstract/Summary:\n{summaryText}";
                var result = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

                var conceptsNode = result?["concepts"]?.AsArray();
                if (conceptsNode == null)
                    continue;

                foreach (var conceptNode in conceptsNode)
                {
                    var typeStr = conceptNode?["type"]?.GetValue<string>();
                    var name = conceptNode?["name"]?.GetValue<string>();
                    var confidence = conceptNode?["confidence"]?.GetValue<double>() ?? 0.5;

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(typeStr))
                        continue;

                    if (!Enum.TryParse<ConceptType>(typeStr, true, out var conceptType))
                        continue;

                    allConcepts.Add(new PaperConcept
                    {
                        PaperId = paper.Id,
                        ConceptType = conceptType,
                        Name = name,
                        Confidence = confidence
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract concepts for paper {PaperId}", paper.Id);
            }
        }

        if (allConcepts.Count > 0)
        {
            dbContext.PaperConcepts.AddRange(allConcepts);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        job.Status = JobStatus.Completed;
        job.ResultJson = JsonSerializer.SerializeToNode(new
        {
            papersProcessed = papers.Count,
            conceptsExtracted = allConcepts.Count,
            completedAt = DateTimeOffset.UtcNow
        })?.ToJsonString();
    }

    private static List<(double X, double Y)> ComputeUmapCoordinates(List<float[]> vectors)
    {
        var n = vectors.Count;
        return ComputeTSneCoordinates(vectors, n, 100, 42);
    }

    private static List<(double X, double Y)> ComputeTSneCoordinates(List<float[]> vectors, int n, int iterations, int seed)
    {
        var coords = new List<(double X, double Y)>();
        var random = new Random(seed);

        for (int i = 0; i < n; i++)
        {
            coords.Add((random.NextDouble() * 0.0001, random.NextDouble() * 0.0001));
        }

        var perplexity = Math.Min(30, n - 1);
        var learningRate = 100.0;

        for (int iter = 0; iter < iterations; iter++)
        {
            var forces = new (double X, double Y)[n];
            for (int i = 0; i < n; i++) forces[i] = (0, 0);

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var sim = VectorMath.CosineSimilarity(vectors[i], vectors[j]);
                    var dx = coords[j].X - coords[i].X;
                    var dy = coords[j].Y - coords[i].Y;
                    var dist = Math.Sqrt(dx * dx + dy * dy) + 1e-10;
                    var force = sim / dist;

                    forces[i].X += dx * force;
                    forces[i].Y += dy * force;
                    forces[j].X -= dx * force;
                    forces[j].Y -= dy * force;
                }
            }

            var momentum = iter < 100 ? 0.5 : 0.8;
            for (int i = 0; i < n; i++)
            {
                coords[i] = (
                    coords[i].X + forces[i].X * learningRate * (1 - momentum) + coords[i].X * momentum * 0.1,
                    coords[i].Y + forces[i].Y * learningRate * (1 - momentum) + coords[i].Y * momentum * 0.1
                );
            }
        }

        if (n < 2) return coords;

        var minX = coords.Min(c => c.X);
        var maxX = coords.Max(c => c.X);
        var minY = coords.Min(c => c.Y);
        var maxY = coords.Max(c => c.Y);

        return coords.Select(c => (
            maxX == minX ? 0.5 : (c.X - minX) / (maxX - minX),
            maxY == minY ? 0.5 : (c.Y - minY) / (maxY - minY)
        )).ToList();
    }

    private static List<float[]> NormalizeVectors(List<float[]> vectors)
    {
        return vectors.Select(v =>
        {
            var mag = Math.Sqrt(v.Sum(x => x * x));
            return mag > 0 ? v.Select(x => (float)(x / mag)).ToArray() : v;
        }).ToList();
    }

    private static JsonObject ParsePayload(Job job) =>
        JsonNode.Parse(job.PayloadJson)?.AsObject()
        ?? throw new InvalidOperationException($"Job {job.Id} has an invalid payload.");
}