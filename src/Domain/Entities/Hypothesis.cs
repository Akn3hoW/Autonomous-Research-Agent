using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class Hypothesis : AuditableEntity
{
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public HypothesisStatus Status { get; set; } = HypothesisStatus.Proposed;
    public string? SupportingEvidenceJson { get; set; }
    public string? RefutingEvidenceJson { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ICollection<HypothesisPaper> HypothesisPapers { get; set; } = [];
}
