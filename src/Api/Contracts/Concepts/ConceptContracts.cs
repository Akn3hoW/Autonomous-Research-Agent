namespace AutonomousResearchAgent.Api.Contracts.Concepts;

public sealed class ConceptQueryRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? ConceptType { get; init; }
    public Guid? PaperId { get; init; }
    public string? Search { get; init; }
}

public sealed record ConceptDto(
    Guid Id,
    Guid PaperId,
    string ConceptType,
    string Name,
    double Confidence,
    DateTimeOffset CreatedAt);

public sealed record ConceptStatisticsDto(
    IReadOnlyList<ConceptTypeCountDto> ByType,
    int TotalConcepts,
    int TotalPapers);

public sealed record ConceptTypeCountDto(
    string ConceptType,
    int Count,
    int PaperCount);
