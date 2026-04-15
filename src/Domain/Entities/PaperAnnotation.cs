using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PaperAnnotation : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid PaperId { get; set; }
    public Guid? DocumentChunkId { get; set; }
    public int? PageNumber { get; set; }
    public int? OffsetStart { get; set; }
    public int? OffsetEnd { get; set; }
    public string HighlightedText { get; set; } = string.Empty;
    public string? Note { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(PaperId))]
    public Paper Paper { get; set; } = null!;

    [ForeignKey(nameof(DocumentChunkId))]
    public DocumentChunk? DocumentChunk { get; set; }
}