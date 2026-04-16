using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.LiteratureReviews;

public sealed record LiteratureReviewSection(
    string Heading,
    string Content,
    IReadOnlyCollection<Guid> CitedPaperIds);

public sealed record LiteratureReviewModel(
    Guid Id,
    Guid UserId,
    string Title,
    string ResearchQuestion,
    string? ContentJson,
    string? ContentMarkdown,
    IReadOnlyList<Guid> PaperIds,
    LiteratureReviewStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record LiteratureReviewDetail(
    Guid Id,
    Guid UserId,
    string Title,
    string ResearchQuestion,
    IReadOnlyList<LiteratureReviewSection> Sections,
    string? ContentMarkdown,
    LiteratureReviewStatus Status,
    IReadOnlyList<Guid> PaperIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record CreateLiteratureReviewCommand(
    string Title,
    string ResearchQuestion,
    IReadOnlyCollection<Guid> PaperIds);