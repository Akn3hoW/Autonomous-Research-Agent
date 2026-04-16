using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Hypotheses;

public interface IHypothesisService
{
    Task<HypothesisResponse> CreateAsync(CreateHypothesisCommand command, CancellationToken cancellationToken);
    Task<HypothesisResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<HypothesisResponse>> GetAllByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<HypothesisResponse> UpdateAsync(Guid id, UpdateHypothesisCommand command, CancellationToken cancellationToken);
    Task<HypothesisResponse> UpdateStatusAsync(Guid id, UpdateHypothesisStatusCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<HypothesisPaperResponse> AddPaperAsync(Guid hypothesisId, AddHypothesisPaperCommand command, CancellationToken cancellationToken);
    Task DeletePaperAsync(Guid hypothesisId, Guid paperId, CancellationToken cancellationToken);
    Task ExtractHypothesesFromAnalysisAsync(Guid analysisResultId, CancellationToken cancellationToken);
}

public sealed record CreateHypothesisCommand(
    string Title,
    string Description,
    Guid UserId,
    List<HypothesisPaperInput>? InitialPapers);

public sealed record UpdateHypothesisCommand(
    string? Title,
    string? Description);

public sealed record UpdateHypothesisStatusCommand(
    HypothesisStatus Status,
    string? EvidenceText);

public sealed record AddHypothesisPaperCommand(
    Guid PaperId,
    EvidenceType EvidenceType,
    string? EvidenceText);

public sealed record HypothesisPaperInput(
    Guid PaperId,
    EvidenceType EvidenceType,
    string? EvidenceText);

public sealed record HypothesisResponse(
    Guid Id,
    string Title,
    string Description,
    HypothesisStatus Status,
    Guid UserId,
    List<HypothesisPaperResponse> SupportingPapers,
    List<HypothesisPaperResponse> RefutingPapers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record HypothesisPaperResponse(
    Guid Id,
    Guid PaperId,
    string PaperTitle,
    EvidenceType EvidenceType,
    string? EvidenceText);

public sealed record HypothesisDetailResponse(
    Guid Id,
    string Title,
    string Description,
    HypothesisStatus Status,
    List<HypothesisPaperResponse> SupportingPapers,
    List<HypothesisPaperResponse> RefutingPapers,
    string? SupportingEvidenceJson,
    string? RefutingEvidenceJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
