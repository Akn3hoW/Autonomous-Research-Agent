using System.ComponentModel;
using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Api.Contracts.Papers;

public sealed record PaperListItemDto(
    Guid Id,
    string Title,
    IReadOnlyCollection<string> Authors,
    int? Year,
    string? Venue,
    int CitationCount,
    string Source,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PaperDetailDto(
    Guid Id,
    string? SemanticScholarId,
    string? Doi,
    string Title,
    string? Abstract,
    IReadOnlyCollection<string> Authors,
    int? Year,
    string? Venue,
    int CitationCount,
    string Source,
    string Status,
    JsonNode? Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class PaperQueryRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Query { get; init; }
    public int? Year { get; init; }
    public string? Venue { get; init; }
    public string? Source { get; init; }
    public string? Status { get; init; }
    public string? SortBy { get; init; }

    [DefaultValue("desc")]
    public string? SortDirection { get; init; } = "desc";
}

public sealed class CreatePaperRequest
{
    public string? SemanticScholarId { get; init; }
    public string? Doi { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Abstract { get; init; }
    public List<string> Authors { get; init; } = [];
    public int? Year { get; init; }
    public string? Venue { get; init; }
    public int CitationCount { get; init; }
    public string Source { get; init; } = "Manual";
    public string Status { get; init; } = "Draft";
    public JsonNode? Metadata { get; init; }
}

public sealed class UpdatePaperRequest
{
    public string? Doi { get; init; }
    public string? Title { get; init; }
    public string? Abstract { get; init; }
    public List<string>? Authors { get; init; }
    public int? Year { get; init; }
    public string? Venue { get; init; }
    public int? CitationCount { get; init; }
    public string? Status { get; init; }
    public JsonNode? Metadata { get; init; }
}

public sealed class ImportPapersRequest
{
    public List<string> Queries { get; init; } = [];
    public int Limit { get; init; } = 10;
    public bool StoreImportedPapers { get; init; } = true;
}

public sealed record ImportPapersResponse(
    IReadOnlyCollection<PaperDetailDto> Papers,
    int ImportedCount);

