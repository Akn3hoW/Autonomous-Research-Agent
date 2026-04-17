namespace AutonomousResearchAgent.Application.Collections;

public interface ICollectionService
{
    Task<IReadOnlyCollection<CollectionListItem>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<CollectionDetail> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<CollectionListItem> CreateAsync(CreateCollectionCommand command, CancellationToken cancellationToken);
    Task<CollectionListItem> UpdateAsync(Guid id, UpdateCollectionCommand command, Guid userId, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task AddPaperAsync(Guid collectionId, AddPaperCommand command, Guid userId, CancellationToken cancellationToken);
    Task RemovePaperAsync(Guid collectionId, RemovePaperCommand command, Guid userId, CancellationToken cancellationToken);
    Task ReorderPapersAsync(Guid collectionId, ReorderPapersCommand command, Guid userId, CancellationToken cancellationToken);
    Task<byte[]> ExportAsync(Guid collectionId, Guid userId, CancellationToken cancellationToken);
    Task<SharedCollectionDetail?> GetSharedCollectionAsync(string token, CancellationToken cancellationToken);
    Task<string> GenerateShareTokenAsync(Guid collectionId, Guid userId, CancellationToken cancellationToken);
    Task RevokeShareTokenAsync(Guid collectionId, Guid userId, CancellationToken cancellationToken);
}