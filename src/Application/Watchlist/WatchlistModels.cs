using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Watchlist;

public sealed record SavedSearchModel(
    Guid Id,
    Guid UserId,
    string Query,
    string? Field,
    ScheduleType Schedule,
    DateTimeOffset? LastRunAt,
    int? ResultCount,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SavedSearchQuery(
    int PageNumber = 1,
    int PageSize = 25,
    Guid? UserId = null,
    bool? IsActive = null);

public sealed record CreateSavedSearchCommand(
    Guid UserId,
    string Query,
    string? Field,
    ScheduleType Schedule);

public sealed record UpdateSavedSearchCommand(
    string? Query,
    string? Field,
    ScheduleType? Schedule,
    bool? IsActive);

public sealed record NotificationModel(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    string? LinkUrl,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed record NotificationQuery(
    int PageNumber = 1,
    int PageSize = 25,
    Guid? UserId = null,
    bool? IsRead = null);

public sealed record RunSavedSearchResult(
    int NewPapersCount,
    Guid? JobId);

public sealed record DigestModel(
    Guid Id,
    Guid UserId,
    DigestFrequency Frequency,
    string Topic,
    string Content,
    int NewPapersCount,
    DateTimeOffset CreatedAt);

public sealed record CreateDigestCommand(
    Guid UserId,
    DigestFrequency Frequency,
    string Topic,
    string Content,
    int NewPapersCount);
