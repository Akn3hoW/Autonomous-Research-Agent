using System.ComponentModel.DataAnnotations;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class ResearchGoalTemplate : AuditableEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public GoalType GoalType { get; set; } = GoalType.General;

    [MaxLength(5000)]
    public string PromptTemplate { get; set; } = string.Empty;

    public string? Parameters { get; set; }

    public bool IsActive { get; set; } = true;
}