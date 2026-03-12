using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Analysis;

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

public sealed record AnalysisResultModel(
    Guid Id,
    Guid? JobId,
    AnalysisType AnalysisType,
    JsonNode? InputSet,
    JsonNode? Result,
    string? CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AnalysisJobStatusModel(
    Guid JobId,
    JobStatus Status,
    string? ErrorMessage,
    AnalysisResultModel? Result);

