namespace AutonomousResearchAgent.Application.Trends;

public interface ITrendAnalysisService
{
    Task<TrendsResponse> GetTrendsAsync(TrendsRequest request, CancellationToken cancellationToken);
    Task<Guid> StartTrendAnalysisJobAsync(string? field, int startYear, int endYear, CancellationToken cancellationToken);
}