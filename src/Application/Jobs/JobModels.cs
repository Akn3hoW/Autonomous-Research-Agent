using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Jobs;

public sealed record JobModel(
    Guid Id,
    JobType Type,
    JobStatus Status,
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

public sealed record JobQuery(
    int PageNumber = 1,
    int PageSize = 25,
    JobType? Type = null,
    JobStatus? Status = null);

public sealed record CreateJobCommand(
    JobType Type,
    JsonNode? Payload,
    Guid? TargetEntityId,
    string? CreatedBy,
    Guid? ParentJobId = null,
    List<Guid>? DependsOnJobIds = null,
    int? WorkflowStep = null);

public sealed record RetryJobCommand(
    string? RequestedBy,
    string? Reason);
