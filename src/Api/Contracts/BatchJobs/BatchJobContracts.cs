namespace AutonomousResearchAgent.Api.Contracts.BatchJobs;

public sealed record BatchJobDto(
    Guid Id,
    string Action,
    string Status,
    int Total,
    int Completed,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class BatchOperationRequest
{
    public string Action { get; init; } = string.Empty;
    public List<Guid> PaperIds { get; init; } = [];
    public Dictionary<string, object> Params { get; init; } = [];
}