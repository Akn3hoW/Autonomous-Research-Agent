using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class AnalysisResult : AuditableEntity
{
    public Guid? JobId { get; set; }
    public AnalysisType AnalysisType { get; set; }
    public string InputSetJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? CreatedBy { get; set; }

    [ForeignKey(nameof(JobId))]
    public Job? Job { get; set; }
}

