using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Api.Contracts.Summaries;

public sealed record SummaryDiffDto(
    Guid PaperId,
    string PaperTitle,
    SummaryVersionDto Summary1,
    SummaryVersionDto Summary2,
    FieldDiffsDto FieldDiffs,
    double OverallSimilarity);

public sealed record SummaryVersionDto(
    Guid Id,
    string ModelName,
    string PromptVersion,
    DateTimeOffset CreatedAt,
    JsonNode? Summary,
    string Status);

public sealed record FieldDiffsDto(
    FieldDiffDto Summary,
    FieldDiffDto ModelName,
    FieldDiffDto PromptVersion);

public sealed record FieldDiffDto(
    string Left,
    string Right,
    string? DiffHtml,
    bool Changed);