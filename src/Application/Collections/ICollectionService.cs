namespace AutonomousResearchAgent.Application.Collections;

public interface ICollectionService
{
    Task<IReadOnlyCollection<CollectionListItem>> ListAsync(int userId, CancellationToken cancellationToken);
    Task<CollectionDetail> GetByIdAsync(Guid id, int userId, CancellationToken cancellationToken);
    Task<CollectionListItem> CreateAsync(CreateCollectionCommand command, CancellationToken cancellationToken);
    Task<CollectionListItem> UpdateAsync(Guid id, UpdateCollectionCommand command, int userId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, int userId, CancellationToken cancellationToken);
    Task AddPaperAsync(Guid collectionId, AddPaperCommand command, int userId, CancellationToken cancellationToken);
    Task RemovePaperAsync(Guid collectionId, RemovePaperCommand command, int userId, CancellationToken cancellationToken);
    Task ReorderPapersAsync(Guid collectionId, ReorderPapersCommand command, int userId, CancellationToken cancellationToken);
    Task<byte[]> ExportAsync(Guid collectionId, int userId, CancellationToken cancellationToken);
    Task<SharedCollectionDetail?> GetSharedCollectionAsync(string token, CancellationToken cancellationToken);
    Task<string> GenerateShareTokenAsync(Guid collectionId, int userId, CancellationToken cancellationToken);
    Task RevokeShareTokenAsync(Guid collectionId, int userId, CancellationToken cancellationToken);
}