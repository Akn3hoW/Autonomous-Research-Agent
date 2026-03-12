using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Application.Papers;

public sealed record SemanticScholarPaperImportModel(
    string SemanticScholarId,
    string? Doi,
    string Title,
    string? Abstract,
    IReadOnlyCollection<string> Authors,
    int? Year,
    string? Venue,
    int CitationCount,
    JsonNode? Metadata);

