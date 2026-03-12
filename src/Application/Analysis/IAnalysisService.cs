namespace AutonomousResearchAgent.Application.Analysis;

public interface IAnalysisService
{
    Task<AnalysisResultModel> ComparePapersAsync(ComparePapersCommand command, CancellationToken cancellationToken);
    Task<AnalysisResultModel> CompareFieldsAsync(CompareFieldsCommand command, CancellationToken cancellationToken);
    Task<AnalysisJobStatusModel> GenerateInsightsAsync(GenerateInsightsCommand command, CancellationToken cancellationToken);
    Task<AnalysisJobStatusModel> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken);
}

