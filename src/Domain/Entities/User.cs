using System.ComponentModel.DataAnnotations;
using AutonomousResearchAgent.Domain.Entities;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class User : AuditableEntity
{
    [Required]
    [MaxLength(256)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
