namespace AutonomousResearchAgent.Application.Audit;

public interface IAuditService
{
    Task<PagedAuditResult> GetAuditLogAsync(AuditLogQuery query, CancellationToken cancellationToken);
    Task LogAuditEventAsync(AuditLogCommand command, CancellationToken cancellationToken);
}

public sealed record AuditLogCommand(
    Guid? UserId,
    string Action,
    string EntityType,
    Guid? EntityId,
    string? OldValues,
    string? NewValues,
    DateTimeOffset Timestamp,
    string? IpAddress);