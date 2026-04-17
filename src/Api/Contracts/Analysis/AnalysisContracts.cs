using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Api.Contracts.Analysis;

public sealed class ComparePapersRequest
{
    public Guid LeftPaperId { get; init; }
    public Guid RightPaperId { get; init; }
}

public sealed class CompareFieldsRequest
{
    public string LeftFilter { get; init; } = string.Empty;
    public string RightFilter { get; init; } = string.Empty;
}

public sealed class GenerateInsightsRequest
{
    public string Filter { get; init; } = string.Empty;
}

public sealed record AnalysisResultDto(
    Guid Id,
    Guid? JobId,
    string AnalysisType,
    JsonNode? InputSet,
    JsonNode? Result,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AnalysisJobStatusDto(
    Guid JobId,
    string Status,
    string? ErrorMessage,
    AnalysisResultDto? Result);

public sealed class IdentifyResearchGapRequest
{
    public string Topic { get; init; } = string.Empty;
}

public sealed record ResearchGapReportDto(
    Guid Id,
    string Topic,
    JsonNode? GapAnalysis,
    JsonNode? CorpusCoverage,
    JsonNode? ExternalCoverage,
    JsonNode? SuggestedQueries,
    string? CreatedBy,
    DateTimeOffset CreatedAt);

