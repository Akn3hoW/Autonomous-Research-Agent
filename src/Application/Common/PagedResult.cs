namespace AutonomousResearchAgent.Application.Common;

public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    long TotalCount);

