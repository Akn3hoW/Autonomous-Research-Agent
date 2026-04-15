using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Papers;

public sealed record PaperListItem(
    Guid Id,
    string Title,
    IReadOnlyCollection<string> Authors,
    int? Year,
    string? Venue,
    int CitationCount,
    PaperSource Source,
    PaperStatus Status,
    IReadOnlyCollection<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PaperDetail(
    Guid Id,
    string? SemanticScholarId,
    string? Doi,
    string Title,
    string? Abstract,
    IReadOnlyCollection<string> Authors,
    int? Year,
    string? Venue,
    int CitationCount,
    PaperSource Source,
    PaperStatus Status,
    JsonNode? Metadata,
    IReadOnlyCollection<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PaperQuery(
    int PageNumber = 1,
    int PageSize = 25,
    string? Query = null,
    int? Year = null,
    string? Venue = null,
    PaperSource? Source = null,
    PaperStatus? Status = null,
    string? Tag = null,
    string? SortBy = null,
    Common.SortDirection SortDirection = Common.SortDirection.Desc);

public sealed record CreatePaperCommand(
    string? SemanticScholarId,
    string? Doi,
    string Title,
    string? Abstract,
    IReadOnlyCollection<string> Authors,
    int? Year,
    string? Venue,
    int CitationCount,
    PaperSource Source,
    PaperStatus Status,
    JsonNode? Metadata);

public sealed record UpdatePaperCommand(
    string? Doi,
    string? Title,
    string? Abstract,
    IReadOnlyCollection<string>? Authors,
    int? Year,
    string? Venue,
    int? CitationCount,
    PaperStatus? Status,
    JsonNode? Metadata);

public sealed record ImportPapersCommand(
    IReadOnlyCollection<string> Queries,
    int Limit,
    bool StoreImportedPapers,
    string Source = "semanticscholar");

public sealed record ImportPapersResult(
    IReadOnlyCollection<PaperDetail> Papers,
    int ImportedCount);

