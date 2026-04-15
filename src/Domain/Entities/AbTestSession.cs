using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class AbTestSession : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid PaperId { get; set; }
    public Guid UserId { get; set; }
    public AbTestSessionStatus Status { get; set; } = AbTestSessionStatus.Running;
    public DateTimeOffset? CompletedAt { get; set; }

    [ForeignKey(nameof(PaperId))]
    public Paper? Paper { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ICollection<PaperSummary> PaperSummaries { get; set; } = [];
}
