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
    DateTimeOffset UpdatedAt,
    Guid? ParentJobId = null,
    int? RetryCount = null,
    DateTimeOffset? LastAttemptAt = null,
    JsonNode? RetryPolicy = null,
    List<Guid>? DependsOnJobIds = null,
    int? WorkflowStep = null);

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
    public List<Guid>? DependsOnJobIds { get; init; }
    public int? WorkflowStep { get; init; }
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

public sealed record DeadLetterJobDto(
    Guid Id,
    Guid OriginalJobId,
    string OriginalJobType,
    JsonNode? OriginalJobPayload,
    string? ErrorMessage,
    string? ExceptionType,
    DateTimeOffset FailedAt,
    int RetryCount,
    string? StackTrace,
    bool IsProcessed,
    Guid? ProcessedBy,
    DateTimeOffset? ProcessedAt,
    string? ProcessingNotes);

public sealed class DeadLetterJobQueryRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? ExceptionType { get; init; }
    public bool? IsProcessed { get; init; }
}

public sealed class RetryDeadLetterJobRequest
{
    public string? Reason { get; init; }
}

public sealed class ProcessDeadLetterJobRequest
{
    public string? Notes { get; init; }
}
