using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class PaperEmbedding : AuditableEntity
{
    public Guid? PaperId { get; set; }
    public Guid? SummaryId { get; set; }
    public Guid? DocumentChunkId { get; set; }
    public EmbeddingType EmbeddingType { get; set; } = EmbeddingType.Paper;
    public float[]? Vector { get; set; }
    public int? VectorDimensions { get; set; }
    public string ModelName { get; set; } = string.Empty;

    public Paper? Paper { get; set; }
    public PaperSummary? Summary { get; set; }
    public DocumentChunk? DocumentChunk { get; set; }
}
