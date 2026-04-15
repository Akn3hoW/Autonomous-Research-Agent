using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Papers;

namespace AutonomousResearchAgent.Application.Summaries;

public interface ISummarizationService
{
    Task<JsonNode?> GenerateSummaryAsync(PaperDetail paper, string modelName, string promptVersion, CancellationToken cancellationToken);
    Task<AbTestSessionModel> CreateAbTestSessionAsync(CreateAbTestRequest request, Guid userId, CancellationToken cancellationToken);
    Task<AbTestSessionModel?> GetAbTestSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AbTestSessionModel>> GetAbTestSessionsForPaperAsync(Guid paperId, CancellationToken cancellationToken);
    Task<AbTestSessionModel?> SelectAbTestResultAsync(Guid sessionId, Guid summaryId, CancellationToken cancellationToken);
}

public sealed record AbTestSessionModel(
    Guid Id,
    string Name,
    Guid PaperId,
    string PaperTitle,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    SummaryResultModel[] Results);

public sealed record SummaryResultModel(
    Guid SummaryId,
    string ModelName,
    string? Summary,
    string SummaryStatus,
    DateTimeOffset CreatedAt,
    bool IsSelected);

public sealed record CreateAbTestRequest(
    string Name,
    Guid PaperId,
    string[] ModelNames);
