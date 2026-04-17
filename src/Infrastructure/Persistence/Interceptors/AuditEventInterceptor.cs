using System.Text.Json;
using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Interceptors;

public sealed class AuditEventInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ProcessChanges(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        ProcessChanges(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ProcessChanges(DbContext? context)
    {
        if (context is null) return;

        var entries = context.ChangeTracker.Entries<AuditableEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            var entityType = entry.Entity.GetType().Name;
            var entityId = entry.Entity.Id;
            string action;
            string? diffJson = null;

            switch (entry.State)
            {
                case EntityState.Added:
                    action = "Created";
                    diffJson = SerializeChanges(entry, null);
                    break;
                case EntityState.Modified:
                    action = "Updated";
                    diffJson = SerializeChanges(entry, entry.Properties.ToDictionary(p => p.Metadata.Name, p => new { Original = p.OriginalValue, Current = p.CurrentValue }));
                    break;
                case EntityState.Deleted:
                    action = "Deleted";
                    diffJson = SerializeChanges(entry, entry.Properties.ToDictionary(p => p.Metadata.Name, p => new { Original = entry.Property(p.Metadata.Name).OriginalValue }));
                    break;
                default:
                    continue;
            }

            var auditEvent = new AuditEvent
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                DiffJson = diffJson
            };

            context.Set<AuditEvent>().Add(auditEvent);
        }
    }

    private static string? SerializeChanges(EntityEntry entry, object? changes)
    {
        if (changes is null) return null;
        try
        {
            return JsonSerializer.Serialize(changes, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}