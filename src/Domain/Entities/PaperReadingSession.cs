using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PaperReadingSession : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid PaperId { get; set; }
    public ReadingStatus Status { get; set; } = ReadingStatus.ToRead;
    public string? Notes { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(PaperId))]
    public Paper Paper { get; set; } = null!;
}
