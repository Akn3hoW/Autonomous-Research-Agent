using System;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Watchlist;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class NotificationService(
    ApplicationDbContext dbContext,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<PagedResult<NotificationModel>> ListAsync(NotificationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var notificationsQuery = dbContext.Notifications.AsNoTracking().AsQueryable();

        if (query.UserId.HasValue)
        {
            notificationsQuery = notificationsQuery.Where(n => n.UserId == query.UserId.Value);
        }

        if (query.IsRead.HasValue)
        {
            notificationsQuery = notificationsQuery.Where(n => n.IsRead == query.IsRead.Value);
        }

        var totalCount = await notificationsQuery.LongCountAsync(cancellationToken);

        var entities = await notificationsQuery
            .OrderByDescending(n => n.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(ToModel).ToList();

        return new PagedResult<NotificationModel>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<NotificationModel> MarkAsReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), id);

        entity.IsRead = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Marked notification {NotificationId} as read", id);
        return ToModel(entity);
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var count = await dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);

        logger.LogInformation("Marked {Count} notifications as read for user {UserId}", count, userId);
        return count;
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }

    private static NotificationModel ToModel(Notification entity) =>
        new(
            entity.Id,
            entity.UserId,
            entity.Title,
            entity.Message,
            entity.LinkUrl,
            entity.IsRead,
            entity.CreatedAt);
}
