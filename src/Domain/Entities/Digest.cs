using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class Digest : AuditableEntity
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    public DigestFrequency Frequency { get; set; } = DigestFrequency.Weekly;

    [Required]
    [MaxLength(500)]
    public string Topic { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int NewPapersCount { get; set; }

    public Guid? SavedSearchId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [ForeignKey(nameof(SavedSearchId))]
    public SavedSearch? SavedSearch { get; set; }
}