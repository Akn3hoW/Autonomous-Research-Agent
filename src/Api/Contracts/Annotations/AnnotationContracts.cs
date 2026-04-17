namespace AutonomousResearchAgent.Api.Contracts.Annotations;

public sealed record AnnotationResponse(
    Guid Id,
    Guid PaperId,
    Guid UserId,
    string UserName,
    string HighlightedText,
    string? Note,
    int? PageNumber,
    int? OffsetStart,
    int? OffsetEnd,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreateAnnotationRequest
{
    public Guid? ChunkId { get; init; }
    public int? Page { get; init; }
    public int? OffsetStart { get; init; }
    public int? OffsetEnd { get; init; }
    public string HighlightedText { get; init; } = string.Empty;
    public string? Note { get; init; }
}

public sealed class UpdateAnnotationRequest
{
    public string? Note { get; init; }
}