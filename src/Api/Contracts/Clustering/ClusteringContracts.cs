namespace AutonomousResearchAgent.Api.Contracts.Clustering;

public sealed record ClusterMapResponseDto(
    IReadOnlyList<PaperClusterDto> Papers,
    int TotalCount);

public sealed record PaperClusterDto(
    Guid Id,
    string Title,
    string? Abstract,
    IReadOnlyList<string> Authors,
    int? Year,
    double X,
    double Y);
