using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class LiteratureReview : AuditableEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ResearchQuestion { get; set; } = string.Empty;
    public string? ContentJson { get; set; }
    public string? ContentMarkdown { get; set; }
    public List<Guid> PaperIds { get; set; } = [];
    public LiteratureReviewStatus Status { get; set; } = LiteratureReviewStatus.Draft;
    public DateTimeOffset? CompletedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}