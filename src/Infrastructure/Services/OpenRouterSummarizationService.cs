using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class OpenRouterSummarizationService(
    ApplicationDbContext dbContext,
    OpenRouterChatClient openRouterChatClient,
    IOptions<OpenRouterOptions> options,
    ILogger<OpenRouterSummarizationService> logger) : ISummarizationService
{
    private readonly OpenRouterOptions _options = options.Value;

    public async Task<JsonNode?> GenerateSummaryAsync(PaperDetail paper, string modelName, string promptVersion, CancellationToken cancellationToken)
    {
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

}
