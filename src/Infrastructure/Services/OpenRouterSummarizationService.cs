using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class OpenRouterSummarizationService(
    ApplicationDbContext dbContext,
    IOpenRouterChatClient openRouterChatClient,
    IOptions<OpenRouterOptions> options,
    ILogger<OpenRouterSummarizationService> logger) : ISummarizationService
{
    private readonly OpenRouterOptions _options = options.Value;

    public async Task<JsonNode?> GenerateSummaryAsync(PaperDetail paper, string modelName, string promptVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(paper);

        var extractedTexts = await dbContext.PaperDocuments
            .AsNoTracking()
            .Where(d => d.PaperId == paper.Id && d.Status == PaperDocumentStatus.Extracted && d.ExtractedText != null)
            .OrderByDescending(d => d.ExtractedText!.Length)
            .Select(d => d.ExtractedText!)
            .Take(3)
            .ToListAsync(cancellationToken);

        var sourceText = extractedTexts.Count > 0
            ? string.Join("\n\n---\n\n", extractedTexts)
            : paper.Abstract ?? string.Empty;

        sourceText = QueryHelpers.Truncate(sourceText, 16000) ?? string.Empty;

        var systemPrompt = """
You are an expert scientific research summarizer.
Return valid JSON only.
Summarize the provided paper faithfully.
Do not invent claims.
The JSON schema must contain:
shortSummary: string
longSummary: string
keyFindings: string[]
methods: string[]
limitations: string[]
tags: string[]
confidence: number
extractedClaims: string[]
evidence: array of objects with fields quote and rationale
""";

        var userPrompt = $"""
Model requested by caller: {modelName}
Configured execution model: {_options.Model}
Prompt version: {promptVersion}

Paper metadata:
- Title: {paper.Title}
- Authors: {string.Join(", ", paper.Authors)}
- Year: {paper.Year}
- Venue: {paper.Venue}
- Abstract: {paper.Abstract}

Source text:
{sourceText}
""";

        logger.LogInformation("Generating OpenRouter summary for paper {PaperId}", paper.Id);
        return await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);
    }

    public async Task<AbTestSessionModel> CreateAbTestSessionAsync(CreateAbTestRequest request, Guid userId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var paper = await dbContext.Papers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.PaperId, cancellationToken)
            ?? throw new NotFoundException(nameof(Paper), request.PaperId);

        var session = new AbTestSession
        {
            Name = request.Name,
            PaperId = request.PaperId,
            UserId = userId,
            Status = AbTestSessionStatus.Running
        };

        dbContext.AbTestSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        var childJobs = new List<Job>();
        foreach (var modelName in request.ModelNames)
        {
            var payload = new JsonObject
            {
                ["paperId"] = session.PaperId,
                ["modelName"] = modelName,
                ["promptVersion"] = "v1",
                ["abTestSessionId"] = session.Id
            };

            var job = new Job
            {
                Type = JobType.SummarizePaper,
                Status = JobStatus.Queued,
                PayloadJson = JsonSerializer.Serialize(payload),
                TargetEntityId = session.PaperId,
                ParentJobId = null
            };

            dbContext.Jobs.Add(job);
            childJobs.Add(job);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var results = childJobs.Select(j => new SummaryResultModel(
            Guid.Empty,
            j.PayloadJson.Contains("modelName") ? JsonNode.Parse(j.PayloadJson)?.AsObject()?["modelName"]?.GetValue<string>() ?? "" : "",
            null,
            "Pending",
            DateTimeOffset.UtcNow,
            false
        )).ToArray();

        return new AbTestSessionModel(
            session.Id,
            session.Name,
            session.PaperId,
            paper.Title,
            session.Status.ToString(),
            session.CreatedAt,
            session.CompletedAt,
            results);
    }

    public async Task<AbTestSessionModel?> GetAbTestSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await dbContext.AbTestSessions
            .AsNoTracking()
            .Include(s => s.Paper)
            .Include(s => s.PaperSummaries)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        var results = session.PaperSummaries.Select(s => new SummaryResultModel(
            s.Id,
            s.ModelName,
            s.SummaryJson,
            s.Status.ToString(),
            s.CreatedAt,
            s.IsSelected
        )).ToArray();

        return new AbTestSessionModel(
            session.Id,
            session.Name,
            session.PaperId,
            session.Paper?.Title ?? string.Empty,
            session.Status.ToString(),
            session.CreatedAt,
            session.CompletedAt,
            results);
    }

    public async Task<IReadOnlyCollection<AbTestSessionModel>> GetAbTestSessionsForPaperAsync(Guid paperId, CancellationToken cancellationToken)
    {
        var sessions = await dbContext.AbTestSessions
            .AsNoTracking()
            .Include(s => s.Paper)
            .Include(s => s.PaperSummaries)
            .Where(s => s.PaperId == paperId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new AbTestSessionModel(
            s.Id,
            s.Name,
            s.PaperId,
            s.Paper?.Title ?? string.Empty,
            s.Status.ToString(),
            s.CreatedAt,
            s.CompletedAt,
            s.PaperSummaries.Select(r => new SummaryResultModel(
                r.Id,
                r.ModelName,
                r.SummaryJson,
                r.Status.ToString(),
                r.CreatedAt,
                r.IsSelected
            )).ToArray()
        )).ToList();
    }

    public async Task<AbTestSessionModel?> SelectAbTestResultAsync(Guid sessionId, Guid summaryId, CancellationToken cancellationToken)
    {
        var session = await dbContext.AbTestSessions
            .Include(s => s.Paper)
            .Include(s => s.PaperSummaries)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session == null)
        {
            return null;
        }

        var summaries = session.PaperSummaries.ToList();
        var selectedSummary = summaries.FirstOrDefault(s => s.Id == summaryId);
        if (selectedSummary == null)
        {
            return null;
        }

        foreach (var summary in summaries)
        {
            summary.IsSelected = summary.Id == summaryId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AbTestSessionModel(
            session.Id,
            session.Name,
            session.PaperId,
            session.Paper?.Title ?? string.Empty,
            session.Status.ToString(),
            session.CreatedAt,
            session.CompletedAt,
            session.PaperSummaries.Select(r => new SummaryResultModel(
                r.Id,
                r.ModelName,
                r.SummaryJson,
                r.Status.ToString(),
                r.CreatedAt,
                r.IsSelected
            )).ToArray()
        );
    }
}
