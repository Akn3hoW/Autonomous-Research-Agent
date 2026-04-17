using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class UserNotificationPreferences : AuditableEntity
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(256)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public DigestFrequency DigestFrequency { get; set; } = DigestFrequency.None;

    public DayOfWeek? DayOfWeek { get; set; }

    public TimeOnly TimeOfDay { get; set; } = new TimeOnly(9, 0);

    public bool Enabled { get; set; } = true;

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}