namespace AutonomousResearchAgent.Application.LiteratureReviews;

public interface ILiteratureReviewService
{
    Task<LiteratureReviewDetail> CreateAsync(CreateLiteratureReviewCommand command, int userId, CancellationToken cancellationToken);
    Task<LiteratureReviewDetail?> GetByIdAsync(Guid id, int userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LiteratureReviewModel>> ListAsync(int userId, CancellationToken cancellationToken);
    Task<string> GenerateReviewContentAsync(Guid reviewId, CancellationToken cancellationToken);
    Task<string> ExportToMarkdownAsync(Guid reviewId, CancellationToken cancellationToken);
    Task<byte[]> ExportToPdfAsync(Guid reviewId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, int userId, CancellationToken cancellationToken);
}