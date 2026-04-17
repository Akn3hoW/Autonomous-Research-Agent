using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

public sealed class Paper : AuditableEntity
{
    public Guid? UserId { get; set; }

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
    public double? ClusterX { get; set; }
    public double? ClusterY { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ICollection<PaperSummary> Summaries { get; set; } = [];
    public ICollection<PaperEmbedding> Embeddings { get; set; } = [];
    public ICollection<PaperDocument> Documents { get; set; } = [];
    public ICollection<PaperConcept> Concepts { get; set; } = [];
    public ICollection<PaperTag> PaperTags { get; set; } = [];
}
