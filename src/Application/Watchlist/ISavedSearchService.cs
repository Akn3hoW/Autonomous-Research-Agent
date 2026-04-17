using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Watchlist;

public interface ISavedSearchService
{
    Task<PagedResult<SavedSearchModel>> ListAsync(SavedSearchQuery query, CancellationToken cancellationToken);
    Task<SavedSearchModel> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SavedSearchModel> CreateAsync(CreateSavedSearchCommand command, CancellationToken cancellationToken);
    Task<SavedSearchModel> UpdateAsync(Guid id, UpdateSavedSearchCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<RunSavedSearchResult> RunAsync(Guid id, CancellationToken cancellationToken);
}

public interface INotificationService
{
    Task<PagedResult<NotificationModel>> ListAsync(NotificationQuery query, CancellationToken cancellationToken);
    Task<NotificationModel> MarkAsReadAsync(Guid id, CancellationToken cancellationToken);
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IDigestService
{
    Task<DigestModel> CreateDigestAsync(CreateDigestCommand command, CancellationToken cancellationToken);
    Task<DigestModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<DigestModel>> GetDigestsForUserAsync(Guid userId, DigestFrequency? frequency, CancellationToken cancellationToken);
    Task<DigestModel?> GetLatestDigestAsync(Guid userId, DigestFrequency frequency, CancellationToken cancellationToken);
    Task<string> GenerateDigestContentAsync(Guid userId, DigestFrequency frequency, CancellationToken cancellationToken);
}
