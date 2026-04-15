namespace AutonomousResearchAgent.Api.Contracts.Collections;

public sealed record CollectionResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsShared,
    int PaperCount,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CollectionDetailResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsShared,
    bool IsPublic,
    string? ShareToken,
    string? ShareUrl,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<CollectionPaperItem> Papers);

public sealed record CollectionPaperItem(
    Guid PaperId,
    string Title,
    IReadOnlyCollection<string> Authors,
    int? Year,
    int SortOrder,
    DateTimeOffset AddedAt);

public sealed record SharedCollectionResponse(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<SharedCollectionPaperItem> Papers);

public sealed record SharedCollectionPaperItem(
    Guid PaperId,
    string Title,
    IReadOnlyCollection<string> Authors,
    int? Year,
    int SortOrder,
    DateTimeOffset AddedAt);

public sealed record ShareCollectionResponse(string ShareToken, string ShareUrl);

public sealed class CreateCollectionRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsShared { get; init; }
}

public sealed class UpdateCollectionRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool? IsShared { get; init; }
}

public sealed class ReorderPapersRequest
{
    public List<Guid> PaperIds { get; init; } = [];
}