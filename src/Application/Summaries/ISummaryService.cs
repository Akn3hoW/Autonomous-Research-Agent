namespace AutonomousResearchAgent.Application.Summaries;

public interface ISummaryService
{
    Task<IReadOnlyCollection<SummaryModel>> ListForPaperAsync(Guid paperId, CancellationToken cancellationToken);
    Task<SummaryModel> GetByIdAsync(Guid summaryId, CancellationToken cancellationToken);
    Task<SummaryModel> CreateAsync(CreateSummaryCommand command, CancellationToken cancellationToken);
    Task<SummaryModel> UpdateAsync(Guid summaryId, UpdateSummaryCommand command, CancellationToken cancellationToken);
    Task<SummaryModel> ReviewAsync(Guid summaryId, ReviewSummaryCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid summaryId, CancellationToken cancellationToken);
}

