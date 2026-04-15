namespace AutonomousResearchAgent.Application.ReadingSessions;

public sealed record ReadingSessionModel(
    Guid Id,
    int UserId,
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
    int UserId,
    Guid PaperId);

public sealed record UpdateReadingSessionCommand(
    string? Status,
    string? Notes);

public sealed record ReadingSessionQuery(
    int UserId,
    string? Status,
    int PageNumber,
    int PageSize);
