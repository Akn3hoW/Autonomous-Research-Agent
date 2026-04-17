namespace AutonomousResearchAgent.Api.Contracts.Watchlist;

public sealed record SavedSearchDto(
    Guid Id,
    Guid UserId,
    string Query,
    string? Field,
    string Schedule,
    DateTimeOffset? LastRunAt,
    int? ResultCount,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class SavedSearchQueryRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public bool? IsActive { get; init; }
}

public sealed class CreateSavedSearchRequest
{
    public string Query { get; init; } = string.Empty;
    public string? Field { get; init; }
    public string Schedule { get; init; } = "Manual";
}

public sealed class UpdateSavedSearchRequest
{
    public string? Query { get; init; }
    public string? Field { get; init; }
    public string? Schedule { get; init; }
    public bool? IsActive { get; init; }
}

public sealed record RunSavedSearchResponse(
    int NewPapersCount,
    Guid? JobId);

public sealed record NotificationDto(
    Guid Id,
    Guid UserId,
    string Title,
    string Message,
    string? LinkUrl,
    bool IsRead,
    DateTimeOffset CreatedAt);

public sealed class NotificationQueryRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public bool? IsRead { get; init; }
}

public sealed record UnreadCountResponse(int Count);

public sealed record MarkAllReadResponse(int MarkedCount);

public sealed record DigestDto(
    Guid Id,
    Guid UserId,
    string Frequency,
    string Topic,
    string Content,
    int NewPapersCount,
    DateTimeOffset CreatedAt);
