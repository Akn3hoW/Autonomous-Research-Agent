namespace AutonomousResearchAgent.Api.Contracts.Hypotheses;

public sealed class HypothesisPaperInput
{
    public Guid PaperId { get; init; }
    public string EvidenceType { get; init; } = "Supporting";
    public string? EvidenceText { get; init; }
}

public sealed class CreateHypothesisRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<HypothesisPaperInput>? InitialPapers { get; init; }
}

public sealed class UpdateHypothesisRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
}

public sealed class UpdateHypothesisStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? EvidenceText { get; init; }
}

public sealed class AddHypothesisPaperRequest
{
    public Guid PaperId { get; init; }
    public string EvidenceType { get; init; } = "Supporting";
    public string? EvidenceText { get; init; }
}

public sealed record HypothesisResponse(
    Guid Id,
    string Title,
    string Description,
    string Status,
    int UserId,
    List<HypothesisPaperResponse> SupportingPapers,
    List<HypothesisPaperResponse> RefutingPapers,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record HypothesisPaperResponse(
    Guid Id,
    Guid PaperId,
    string PaperTitle,
    string EvidenceType,
    string? EvidenceText);

public sealed record HypothesisDetailResponse(
    Guid Id,
    string Title,
    string Description,
    string Status,
    List<HypothesisPaperResponse> SupportingPapers,
    List<HypothesisPaperResponse> RefutingPapers,
    string? SupportingEvidenceJson,
    string? RefutingEvidenceJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
