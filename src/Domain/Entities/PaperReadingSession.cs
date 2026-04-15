using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PaperReadingSession : AuditableEntity
{
    public int UserId { get; set; }
    public Guid PaperId { get; set; }
    public ReadingStatus Status { get; set; } = ReadingStatus.ToRead;
    public string? Notes { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public User User { get; set; } = null!;
    public Paper Paper { get; set; } = null!;
}
