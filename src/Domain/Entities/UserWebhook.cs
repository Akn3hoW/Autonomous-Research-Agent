using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class UserWebhook : AuditableEntity
{
    public int UserId { get; set; }
    public string Url { get; set; } = string.Empty;
    public List<string> Events { get; set; } = [];
    public string Secret { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
