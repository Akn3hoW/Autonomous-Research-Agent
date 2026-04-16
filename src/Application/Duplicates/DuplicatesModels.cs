using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Duplicates;

public sealed record DuplicatePairModel(
    Guid Id,
    Guid PaperAId,
    string PaperATitle,
    Guid PaperBId,
    string PaperBTitle,
    double SimilarityScore,
    DuplicateReviewStatus Status,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record DuplicatesResult(
    IReadOnlyList<DuplicatePairModel> Pairs,
    int TotalCount,
    int PendingCount);

public sealed record DuplicateDetectionCommand(
    double Threshold,
    string? RequestedBy);

public sealed record ResolveDuplicateCommand(
    Guid DuplicateId,
    bool IsDuplicate,
    Guid? MergedIntoPaperId,
    string? Notes,
    Guid? ReviewedByUserId);

public sealed record MergeDuplicateCommand(
    Guid KeepPaperId,
    Guid MergeIntoPaperId,
    string? Notes,
    Guid? ReviewedByUserId);
