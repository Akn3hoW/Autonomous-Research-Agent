using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class CollectionPaper : AuditableEntity
{
    public Guid CollectionId { get; set; }
    public Guid PaperId { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    [ForeignKey(nameof(CollectionId))]
    public Collection Collection { get; set; } = null!;

    [ForeignKey(nameof(PaperId))]
    public Paper Paper { get; set; } = null!;
}