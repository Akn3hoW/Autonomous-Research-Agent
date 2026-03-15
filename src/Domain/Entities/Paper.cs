using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class Paper : AuditableEntity
{
    public string? SemanticScholarId { get; set; }
    public string? Doi { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Abstract { get; set; }
    public List<string> Authors { get; set; } = [];
    public int? Year { get; set; }
    public string? Venue { get; set; }
    public int CitationCount { get; set; }
    public PaperSource Source { get; set; } = PaperSource.Manual;
    public PaperStatus Status { get; set; } = PaperStatus.Draft;
    public string? MetadataJson { get; set; }

    public ICollection<PaperSummary> Summaries { get; set; } = [];
    public ICollection<PaperEmbedding> Embeddings { get; set; } = [];
    public ICollection<PaperDocument> Documents { get; set; } = [];
}

