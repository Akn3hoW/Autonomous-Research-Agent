namespace AutonomousResearchAgent.Api.Contracts.Admin;

public sealed record AuditLogRequest(
    int PageNumber = 1,
    int PageSize = 50,
    Guid? UserId = null,
    string? EntityType = null,
    string? Action = null,
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null);

public sealed record AuditEventDto(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string EntityType,
    Guid? EntityId,
    string Action,
    object? Diff,
    DateTimeOffset Timestamp);

public sealed record AuditLogResponse(
    IReadOnlyList<AuditEventDto> Items,
    int PageNumber,
    int PageSize,
    long TotalCount);