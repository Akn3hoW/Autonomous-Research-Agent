using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PaperCitation : AuditableEntity
{
    public Guid SourcePaperId { get; set; }
    public Guid TargetPaperId { get; set; }
    public string? CitationContext { get; set; }
    public DateTime IngestedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SourcePaperId))]
    public Paper SourcePaper { get; set; } = null!;

    [ForeignKey(nameof(TargetPaperId))]
    public Paper TargetPaper { get; set; } = null!;
}
