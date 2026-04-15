using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Concepts;

public sealed record ConceptModel(
    Guid Id,
    Guid PaperId,
    ConceptType ConceptType,
    string Name,
    double Confidence,
    DateTimeOffset CreatedAt);

public sealed record ConceptQuery(
    int PageNumber = 1,
    int PageSize = 50,
    ConceptType? ConceptType = null,
    Guid? PaperId = null,
    string? Search = null);

public sealed record PaperWithClusterMap(
    Guid Id,
    string Title,
    string? Abstract,
    IReadOnlyCollection<string> Authors,
    int? Year,
    double? ClusterX,
    double? ClusterY);

public sealed record ClusterMapResult(
    IReadOnlyList<PaperWithClusterMap> Papers,
    int TotalCount);

public sealed record ConceptStatistics(
    IReadOnlyList<ConceptTypeCount> ByType,
    int TotalConcepts,
    int TotalPapers);

public sealed record ConceptTypeCount(
    ConceptType ConceptType,
    int Count,
    int PaperCount);
