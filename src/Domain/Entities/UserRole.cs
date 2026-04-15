using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class UserRole : AuditableEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(RoleId))]
    public Role Role { get; set; } = null!;
}
