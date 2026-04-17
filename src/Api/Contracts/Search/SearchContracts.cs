using System.ComponentModel;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Contracts.Search;

public sealed class SearchRequest
{
    [FromQuery(Name = "q")]
    public string Query { get; init; } = string.Empty;

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class SemanticSearchRequest
{
    public string Query { get; init; } = string.Empty;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int MaxCandidates { get; init; } = 50;
}

public sealed class HybridSearchRequest
{
    public string Query { get; init; } = string.Empty;

    [DefaultValue(0.5)]
    public double KeywordWeight { get; init; } = 0.5;

    [DefaultValue(0.5)]
    public double SemanticWeight { get; init; } = 0.5;

    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public int MaxCandidates { get; init; } = 50;
}

public sealed record SearchResultDto(
    Guid PaperId,
    string Title,
    string? Abstract,
    IReadOnlyCollection<string> Authors,
    int? Year,
    string? Venue,
    double Score,
    string MatchType,
    JsonNode? Highlights);

public sealed class ChunkSearchRequest
{
    public string Query { get; init; } = string.Empty;
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int MaxCandidates { get; init; } = 50;
}

public sealed record ChunkSearchResultDto(
    Guid ChunkId,
    Guid PaperId,
    string PaperTitle,
    string ChunkText,
    double Score);

