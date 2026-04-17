using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Analysis;

public sealed record PaperComparisonResult(
    string OverlapSummary,
    IReadOnlyList<string> ContradictionHints,
    IReadOnlyList<string> NoveltyHints,
    IReadOnlyList<string> CommonThemes,
    string FieldBridgingNotes,
    JsonObject? ScoringMetadata);

public sealed record ResearchGapResult(
    IReadOnlyList<string> UnderstudiedAngles,
    IReadOnlyList<string> ResearchOpportunities,
    IReadOnlyList<string> SuggestedQueries,
    JsonObject? CoverageGaps,
    string? ComparisonSummary);

public sealed record InsightResult(
    string Theme,
    IReadOnlyList<string> KeyFindings,
    IReadOnlyList<string> Evidence,
    JsonObject? SupportingPapers,
    double ConfidenceScore);

public sealed record TrendAnalysisResult(
    string TrendName,
    IReadOnlyList<string> RelatedPapers,
    IReadOnlyList<string> Timeline,
    string Prediction,
    double Certainty);

public sealed record FieldComparisonResult(
    string LeftField,
    string RightField,
    string OverlapSummary,
    IReadOnlyList<string> ContradictionHints,
    IReadOnlyList<string> NoveltyHints,
    IReadOnlyList<string> CommonThemes,
    string FieldBridgingNotes);

public sealed record ComparePapersCommand(
    Guid LeftPaperId,
    Guid RightPaperId,
    string? RequestedBy);

public sealed record CompareFieldsCommand(
    string LeftFilter,
    string RightFilter,
    string? RequestedBy);

public sealed record GenerateInsightsCommand(
    string Filter,
    string? RequestedBy);

public sealed record IdentifyResearchGapCommand(
    string Topic,
    string? RequestedBy);

public sealed record AnalysisResultModel(
    Guid Id,
    Guid? JobId,
    AnalysisType AnalysisType,
    JsonNode? InputSet,
    JsonNode? Result,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public PaperComparisonResult? ToPaperComparison() => Result is JsonObject obj
        ? new PaperComparisonResult(
            obj["overlapSummary"]?.GetValue<string>() ?? string.Empty,
            obj["contradictionHints"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["noveltyHints"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["commonThemes"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["fieldBridgingNotes"]?.GetValue<string>() ?? string.Empty,
            obj["scoringMetadata"] as JsonObject)
        : null;

    public ResearchGapResult? ToResearchGap() => Result is JsonObject obj
        ? new ResearchGapResult(
            obj["understudiedAngles"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["researchOpportunities"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["suggestedQueries"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["coverageGaps"] as JsonObject,
            obj["comparisonSummary"]?.GetValue<string>())
        : null;
}

public sealed record AnalysisJobStatusModel(
    Guid JobId,
    JobStatus Status,
    string? ErrorMessage,
    AnalysisResultModel? Result);

public sealed record ResearchGapReportModel(
    Guid Id,
    string Topic,
    JsonNode? GapAnalysis,
    JsonNode? CorpusCoverage,
    JsonNode? ExternalCoverage,
    JsonNode? SuggestedQueries,
    string? CreatedBy,
    DateTimeOffset CreatedAt)
{
    public ResearchGapResult? ToResearchGapResult() => GapAnalysis is JsonObject obj
        ? new ResearchGapResult(
            obj["understudiedAngles"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["researchOpportunities"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["suggestedQueries"]?.AsArray().Select(x => x?.GetValue<string>() ?? string.Empty).ToList() ?? [],
            obj["coverageGaps"] as JsonObject,
            obj["comparisonSummary"]?.GetValue<string>())
        : null;
}
