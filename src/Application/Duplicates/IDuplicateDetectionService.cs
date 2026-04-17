namespace AutonomousResearchAgent.Application.Duplicates;

public interface IDuplicateDetectionService
{
    Task<Guid> StartDuplicateDetectionJobAsync(double threshold = 0.95, CancellationToken cancellationToken = default);
    Task<DuplicatesResult> GetPotentialDuplicatesAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<DuplicatesResult> GetPendingDuplicatesAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task ResolveDuplicateAsync(Guid duplicateId, bool isDuplicate, Guid? mergedIntoPaperId, string? notes, Guid? reviewedByUserId, CancellationToken cancellationToken = default);
    Task MergeDuplicatePapersAsync(Guid keepPaperId, Guid mergeIntoPaperId, string? notes, Guid? reviewedByUserId, CancellationToken cancellationToken = default);
    Task ComputeDuplicatePairsAsync(double threshold, CancellationToken cancellationToken = default);
}
