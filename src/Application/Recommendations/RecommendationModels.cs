namespace AutonomousResearchAgent.Application.Recommendations;

public sealed record PaperRecommendationModel(
    Guid PaperId,
    string Title,
    List<string> Authors,
    int? Year,
    string? Venue,
    int CitationCount,
    string Status,
    double SimilarityScore,
    DateTimeOffset CreatedAt);

public sealed record RecommendationQuery(
    int UserId,
    int PageNumber,
    int PageSize,
    int MaxRecommendations = 50);
