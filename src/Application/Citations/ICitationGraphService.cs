namespace AutonomousResearchAgent.Application.Citations;

public interface ICitationGraphService
{
    Task<CitationGraphDto> GetCitationGraphAsync(Guid paperId, int depth = 2, CancellationToken cancellationToken = default);
    Task IngestCitationsAsync(Guid paperId, CancellationToken cancellationToken = default);
}
