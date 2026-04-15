namespace AutonomousResearchAgent.Application.Audit;

public interface IAuditService
{
    Task<PagedAuditResult> GetAuditLogAsync(AuditLogQuery query, CancellationToken cancellationToken);
}