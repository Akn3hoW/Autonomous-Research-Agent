using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PaperTag : AuditableEntity
{
    [Required]
    [MaxLength(100)]
    public string Tag { get; set; } = string.Empty;

    public Guid PaperId { get; set; }

    public Guid UserId { get; set; }

    [ForeignKey(nameof(PaperId))]
    public Paper Paper { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
