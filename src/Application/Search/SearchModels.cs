using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Application.Search;

public sealed record SearchRequestModel(
    string Query,
    int PageNumber = 1,
    int PageSize = 25);

public sealed record SemanticSearchRequestModel(
    string Query,
    int PageNumber = 1,
    int PageSize = 25,
    int MaxCandidates = 50);

public sealed record HybridSearchRequestModel(
    string Query,
    double KeywordWeight = 0.5,
    double SemanticWeight = 0.5,
    int PageNumber = 1,
    int PageSize = 25,
    int MaxCandidates = 50);

public sealed record SearchResultModel(
    Guid PaperId,
    string Title,
    string? Abstract,
    IReadOnlyCollection<string> Authors,
    int? Year,
    string? Venue,
    double Score,
    string MatchType,
    JsonNode? Highlights);

public sealed record ChunkSearchRequestModel(
    string Query,
    int PageNumber = 1,
    int PageSize = 10,
    int MaxCandidates = 50);

public sealed record ChunkSearchResultModel(
    Guid ChunkId,
    Guid PaperId,
    string PaperTitle,
    string ChunkText,
    double Score);

