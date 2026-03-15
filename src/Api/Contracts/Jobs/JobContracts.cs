using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Api.Contracts.Jobs;

public sealed record JobDto(
    Guid Id,
    string Type,
    string Status,
    JsonNode? Payload,
    JsonNode? Result,
    string? ErrorMessage,
    Guid? TargetEntityId,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class JobQueryRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Type { get; init; }
    public string? Status { get; init; }
}

public sealed class CreateJobRequest
{
    public string Type { get; init; } = string.Empty;
    public JsonNode? Payload { get; init; }
    public Guid? TargetEntityId { get; init; }
}

public sealed class CreateImportJobRequest
{
    public List<string> Queries { get; init; } = [];
    public int Limit { get; init; } = 10;
    public bool StoreImportedPapers { get; init; } = true;
}

public sealed class CreateSummarizeJobRequest
{
    public Guid PaperId { get; init; }
    public string ModelName { get; init; } = "openrouter/hunter-alpha";
    public string PromptVersion { get; init; } = string.Empty;
}

public sealed class RetryJobRequest
{
    public string? Reason { get; init; }
}

