using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class DeadLetterJob
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OriginalJobId { get; set; }

    [ForeignKey(nameof(OriginalJobId))]
    public Job? OriginalJob { get; set; }

    [Required]
    [MaxLength(64)]
    public string OriginalJobType { get; set; } = string.Empty;

    [Column(TypeName = "jsonb")]
    public string OriginalJobPayload { get; set; } = "{}";

    [MaxLength(4096)]
    public string? ErrorMessage { get; set; }

    [MaxLength(256)]
    public string? ExceptionType { get; set; }

    public DateTimeOffset FailedAt { get; set; } = DateTimeOffset.UtcNow;

    public int RetryCount { get; set; }

    public string? StackTrace { get; set; }

    public bool IsProcessed { get; set; }

    public Guid? ProcessedBy { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    [MaxLength(512)]
    public string? ProcessingNotes { get; set; }
}
