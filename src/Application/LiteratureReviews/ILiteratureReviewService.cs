namespace AutonomousResearchAgent.Application.LiteratureReviews;

public interface ILiteratureReviewService
{
    Task<LiteratureReviewDetail> CreateAsync(CreateLiteratureReviewCommand command, Guid userId, CancellationToken cancellationToken);
    Task<LiteratureReviewDetail?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LiteratureReviewModel>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<string> GenerateReviewContentAsync(Guid reviewId, CancellationToken cancellationToken);
    Task<string> ExportToMarkdownAsync(Guid reviewId, CancellationToken cancellationToken);
    Task<byte[]> ExportToPdfAsync(Guid reviewId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}