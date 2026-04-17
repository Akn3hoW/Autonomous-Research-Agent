namespace AutonomousResearchAgent.Application.Citations;

public sealed record PaperNode(
    int Id,
    string Title,
    int? Year,
    int CitationCount,
    bool IsInDatabase);

public sealed record CitationEdge(
    int SourceId,
    int TargetId,
    string? Context);

public sealed record CitationGraphDto(
    List<PaperNode> Nodes,
    List<CitationEdge> Edges);
