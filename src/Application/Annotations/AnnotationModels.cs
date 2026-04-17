namespace AutonomousResearchAgent.Application.Annotations;

public sealed record AnnotationModel(
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

public sealed record CreateAnnotationCommand(
    Guid PaperId,
    Guid UserId,
    Guid? DocumentChunkId,
    int? PageNumber,
    int? OffsetStart,
    int? OffsetEnd,
    string HighlightedText,
    string? Note);

public sealed record UpdateAnnotationCommand(
    string? Note);