using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class Role : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
