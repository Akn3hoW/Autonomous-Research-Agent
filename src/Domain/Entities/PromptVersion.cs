using System.ComponentModel.DataAnnotations;
using AutonomousResearchAgent.Domain.Entities;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PromptVersion : AuditableEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    [Required]
    public string SystemPrompt { get; set; } = string.Empty;

    [Required]
    public string UserPromptTemplate { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<PaperSummary> PaperSummaries { get; set; } = [];
}