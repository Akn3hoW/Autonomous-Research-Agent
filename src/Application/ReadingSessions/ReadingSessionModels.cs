namespace AutonomousResearchAgent.Application.ReadingSessions;

public sealed record ReadingSessionModel(
    Guid Id,
    Guid UserId,
    Guid PaperId,
    string PaperTitle,
    List<string> PaperAuthors,
    int? PaperYear,
    string? PaperVenue,
    int PaperCitationCount,
    string Status,
    string? Notes,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateReadingSessionCommand(
    Guid UserId,
    Guid PaperId);

public sealed record UpdateReadingSessionCommand(
    string? Status,
    string? Notes);

public sealed record ReadingSessionQuery(
    Guid UserId,
    string? Status,
    int PageNumber,
    int PageSize);
