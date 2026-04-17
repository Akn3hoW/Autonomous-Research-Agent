using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class BatchJob : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public BatchJobStatus Status { get; set; } = BatchJobStatus.Pending;
    public int Total { get; set; }
    public int Completed { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}

public enum BatchJobStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}