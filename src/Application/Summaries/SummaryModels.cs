using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Summaries;

public sealed record SummaryModel(
    Guid Id,
    Guid PaperId,
    string ModelName,
    string PromptVersion,
    SummaryStatus Status,
    JsonNode? Summary,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewNotes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateSummaryCommand(
    Guid PaperId,
    string ModelName,
    string PromptVersion,
    SummaryStatus Status,
    JsonNode? Summary,
    string? SearchText);

public sealed record UpdateSummaryCommand(
    SummaryStatus? Status,
    JsonNode? Summary,
    string? SearchText);

public sealed record ReviewSummaryCommand(
    SummaryStatus Status,
    string? Reviewer,
    string? Notes);

