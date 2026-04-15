using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PotentialDuplicate : AuditableEntity
{
    public Guid PaperAId { get; set; }
    public Guid PaperBId { get; set; }
    public double SimilarityScore { get; set; }
    public DuplicateReviewStatus Status { get; set; } = DuplicateReviewStatus.Pending;
    public int? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Notes { get; set; }

    [ForeignKey(nameof(PaperAId))]
    public Paper? PaperA { get; set; }

    [ForeignKey(nameof(PaperBId))]
    public Paper? PaperB { get; set; }

    [ForeignKey(nameof(ReviewedByUserId))]
    public User? ReviewedByUser { get; set; }
}
