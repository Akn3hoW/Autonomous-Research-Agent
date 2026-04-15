using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PaperConcept : AuditableEntity
{
    public Guid PaperId { get; set; }
    public ConceptType ConceptType { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Confidence { get; set; }

    public Paper? Paper { get; set; }
}
