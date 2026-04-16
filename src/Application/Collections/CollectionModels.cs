namespace AutonomousResearchAgent.Application.Collections;

public sealed record CollectionListItem(
    Guid Id,
    string Name,
    string? Description,
    bool IsShared,
    int PaperCount,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CollectionDetail(
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
    IReadOnlyCollection<CollectionPaperDetail> Papers);

public sealed record CollectionPaperDetail(
    Guid PaperId,
    string Title,
    IReadOnlyCollection<string> Authors,
    int? Year,
    int SortOrder,
    DateTimeOffset AddedAt);

public sealed record CreateCollectionCommand(
    Guid UserId,
    string Name,
    string? Description,
    bool IsShared);

public sealed record UpdateCollectionCommand(
    string? Name,
    string? Description,
    bool? IsShared);

public sealed record ReorderPapersCommand(
    IReadOnlyCollection<Guid> PaperIds);

public sealed record AddPaperCommand(Guid PaperId);

public sealed record RemovePaperCommand(Guid PaperId);

public sealed record SharedCollectionDetail(
    Guid Id,
    string Name,
    string? Description,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<SharedCollectionPaperDetail> Papers);

public sealed record SharedCollectionPaperDetail(
    Guid PaperId,
    string Title,
    IReadOnlyCollection<string> Authors,
    int? Year,
    int SortOrder,
    DateTimeOffset AddedAt);