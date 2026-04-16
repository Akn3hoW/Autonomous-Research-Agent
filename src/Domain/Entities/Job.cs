using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class Job : AuditableEntity
{
    public JobType Type { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Queued;

    public string PayloadJson { get; set; } = "{}";

    public string? ResultJson { get; set; }

    public string? ErrorMessage { get; set; }
    public Guid? TargetEntityId { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? ParentJobId { get; set; }

    [ForeignKey(nameof(ParentJobId))]
    public Job? ParentJob { get; set; }

    public ICollection<Job> ChildJobs { get; set; } = [];

    public int? RetryCount { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    [Column(TypeName = "jsonb")]
    public string? RetryPolicyJson { get; set; }

    public DateTimeOffset? CancellationRequestedAt { get; set; }

    public int? TimeoutSeconds { get; set; }

    [Column(TypeName = "jsonb")]
    public string? DependsOnJobIds { get; set; }

    public int? WorkflowStep { get; set; }
}
