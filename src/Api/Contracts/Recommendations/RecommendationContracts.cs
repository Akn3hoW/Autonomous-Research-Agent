namespace AutonomousResearchAgent.Api.Contracts.Recommendations;

public sealed record PaperRecommendationResponse(
    Guid PaperId,
    string Title,
    List<string> Authors,
    int? Year,
    string? Venue,
    int CitationCount,
    string Status,
    double SimilarityScore,
    DateTimeOffset CreatedAt);
