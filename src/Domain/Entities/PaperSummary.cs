using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PaperSummary : AuditableEntity
{
    public Guid PaperId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string PromptVersion { get; set; } = string.Empty;
    public SummaryStatus Status { get; set; } = SummaryStatus.Pending;
    public string? SummaryJson { get; set; }
    public string? SearchText { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? AbTestSessionId { get; set; }
    public bool IsSelected { get; set; }

    [ForeignKey(nameof(PaperId))]
    public Paper? Paper { get; set; }

    [ForeignKey(nameof(AbTestSessionId))]
    public AbTestSession? AbTestSession { get; set; }

    public ICollection<PaperEmbedding> Embeddings { get; set; } = [];
}
