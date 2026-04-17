using System.ComponentModel;

namespace AutonomousResearchAgent.Api.Contracts.LiteratureReviews;

public sealed record LiteratureReviewSection(
    string Heading,
    string Content,
    IReadOnlyCollection<Guid> CitedPaperIds);

public sealed record LiteratureReviewDto(
    Guid Id,
    string Title,
    string ResearchQuestion,
    IReadOnlyList<LiteratureReviewSection> Sections,
    string Status,
    IReadOnlyList<Guid> PaperIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed record LiteratureReviewListItemDto(
    Guid Id,
    string Title,
    string ResearchQuestion,
    string Status,
    int PaperCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed class CreateLiteratureReviewRequest
{
    public string Title { get; init; } = string.Empty;
    public string ResearchQuestion { get; init; } = string.Empty;
    public List<Guid> PaperIds { get; init; } = [];
}