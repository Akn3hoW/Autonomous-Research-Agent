namespace AutonomousResearchAgent.Application.Audit;

public sealed record AuditEventModel(
    Guid Id,
    Guid? UserId,
    string? UserName,
    string EntityType,
    Guid? EntityId,
    string Action,
    string? DiffJson,
    DateTimeOffset Timestamp);

public sealed record AuditLogQuery(
    int PageNumber,
    int PageSize,
    Guid? UserId,
    string? EntityType,
    string? Action,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate);

public sealed record PagedAuditResult(
    IReadOnlyList<AuditEventModel> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);