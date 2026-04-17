using AutonomousResearchAgent.Application.Audit;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class AuditService(DbContext dbContext) : IAuditService
{
    public async Task<PagedAuditResult> GetAuditLogAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var q = dbContext.Set<AuditEvent>().AsNoTracking();

        if (query.UserId.HasValue)
            q = q.Where(e => e.UserId == query.UserId.Value);

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            q = q.Where(e => e.EntityType == query.EntityType);

        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(e => e.Action == query.Action);

        if (query.StartDate.HasValue)
            q = q.Where(e => e.Timestamp >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            q = q.Where(e => e.Timestamp <= query.EndDate.Value);

        var totalCount = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(e => e.Timestamp)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(e => e.User)
            .ToListAsync(cancellationToken);

        var models = items.Select(e => new AuditEventModel(
            e.Id,
            e.UserId,
            e.User?.Username,
            e.EntityType,
            e.EntityId,
            e.Action,
            e.DiffJson,
            e.Timestamp)).ToList();

        return new PagedAuditResult(models, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task LogAuditEventAsync(AuditLogCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var auditEvent = new AuditEvent
        {
            UserId = command.UserId,
            Action = command.Action,
            EntityType = command.EntityType,
            EntityId = command.EntityId,
            DiffJson = BuildDiffJson(command.OldValues, command.NewValues),
            Timestamp = command.Timestamp,
            IpAddress = command.IpAddress
        };

        dbContext.Set<AuditEvent>().Add(auditEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? BuildDiffJson(string? oldValues, string? newValues)
    {
        if (string.IsNullOrWhiteSpace(oldValues) && string.IsNullOrWhiteSpace(newValues))
            return null;

        return System.Text.Json.JsonSerializer.Serialize(new { Old = oldValues, New = newValues });
    }
}