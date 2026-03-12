namespace AutonomousResearchAgent.Api.Contracts.Common;

public sealed record PagedResponse<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount);

