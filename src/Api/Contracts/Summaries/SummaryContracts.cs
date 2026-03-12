using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Api.Contracts.Summaries;

public sealed record SummaryDto(
    Guid Id,
    Guid PaperId,
    string ModelName,
    string PromptVersion,
    string Status,
    JsonNode? Summary,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateSummaryRequest
{
    public string ModelName { get; init; } = string.Empty;
    public string PromptVersion { get; init; } = string.Empty;
    public string Status { get; init; } = "Generated";
    public JsonNode? Summary { get; init; }
    public string? SearchText { get; init; }
}

public sealed class UpdateSummaryRequest
{
    public string? Status { get; init; }
    public JsonNode? Summary { get; init; }
    public string? SearchText { get; init; }
}

public sealed class ReviewSummaryRequest
{
    public string? Notes { get; init; }
}

