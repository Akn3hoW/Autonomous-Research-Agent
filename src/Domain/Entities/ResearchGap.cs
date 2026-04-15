namespace AutonomousResearchAgent.Domain.Entities;

public sealed class ResearchGap : AuditableEntity
{
    public string Topic { get; set; } = string.Empty;
    public string? GapAnalysisJson { get; set; }
    public string? CorpusCoverageJson { get; set; }
    public string? ExternalCoverageJson { get; set; }
    public string? SuggestedQueriesJson { get; set; }
    public string? CreatedBy { get; set; }
}