using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class HypothesisPaper : AuditableEntity
{
    public Guid HypothesisId { get; set; }
    public Guid PaperId { get; set; }
    public EvidenceType EvidenceType { get; set; }
    public string? EvidenceText { get; set; }

    [ForeignKey(nameof(HypothesisId))]
    public Hypothesis Hypothesis { get; set; } = null!;

    [ForeignKey(nameof(PaperId))]
    public Paper Paper { get; set; } = null!;
}
