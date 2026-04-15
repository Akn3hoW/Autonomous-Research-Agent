namespace AutonomousResearchAgent.Api.Contracts.ReadingSessions;

public sealed record ReadingSessionResponse(
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

public sealed record CreateReadingSessionRequest(
    Guid PaperId);

public sealed record UpdateReadingSessionRequest(
    string? Status,
    string? Notes);
