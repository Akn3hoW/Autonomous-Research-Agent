using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Api.Contracts.Papers;

public sealed record DuplicatePairResponse(
    Guid Id,
    Guid PaperAId,
    string PaperATitle,
    Guid PaperBId,
    string PaperBTitle,
    double SimilarityScore,
    string Status,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? Notes,
    DateTimeOffset CreatedAt);

public sealed record DuplicatesResponse(
    IReadOnlyList<DuplicatePairResponse> Pairs,
    int TotalCount,
    int PendingCount);

public sealed record ResolveDuplicateRequest
{
    public bool IsDuplicate { get; init; }
    public Guid? MergedIntoPaperId { get; init; }
    public string? Notes { get; init; }
}

public sealed record MergeDuplicateRequest
{
    public Guid KeepPaperId { get; init; }
    public Guid MergeIntoPaperId { get; init; }
    public string? Notes { get; init; }
}
