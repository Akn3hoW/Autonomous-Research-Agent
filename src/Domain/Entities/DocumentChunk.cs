using System.ComponentModel.DataAnnotations.Schema;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class DocumentChunk : AuditableEntity
{
    public Guid PaperDocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public int TextLength { get; set; }
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }

    [ForeignKey(nameof(PaperDocumentId))]
    public PaperDocument PaperDocument { get; set; } = null!;

    public ICollection<PaperEmbedding> Embeddings { get; set; } = [];
}
