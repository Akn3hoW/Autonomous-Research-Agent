namespace AutonomousResearchAgent.Application.Annotations;

public interface IAnnotationService
{
    Task<IReadOnlyCollection<AnnotationModel>> ListForPaperAsync(Guid paperId, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<AnnotationModel> GetByIdAsync(Guid annotationId, CancellationToken cancellationToken);
    Task<AnnotationModel> CreateAsync(CreateAnnotationCommand command, CancellationToken cancellationToken);
    Task<AnnotationModel> UpdateAsync(Guid annotationId, UpdateAnnotationCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid annotationId, CancellationToken cancellationToken);
}