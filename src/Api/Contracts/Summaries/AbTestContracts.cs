namespace AutonomousResearchAgent.Api.Contracts.Summaries;

public sealed class CreateAbTestRequest
{
    public string Name { get; init; } = string.Empty;
    public Guid PaperId { get; init; }
    public string[] ModelNames { get; init; } = [];
}

public sealed record AbTestSessionDto(
    Guid Id,
    string Name,
    Guid PaperId,
    string PaperTitle,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    SummaryResultDto[] Results);

public sealed record SummaryResultDto(
    Guid SummaryId,
    string ModelName,
    string? Summary,
    string SummaryStatus,
    DateTimeOffset CreatedAt,
    bool IsSelected);
