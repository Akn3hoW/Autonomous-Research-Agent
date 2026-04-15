using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

/// <summary>
/// Represents a paper document with processing state, extracted text, and optional OCR fallback.
/// </summary>
public sealed class PaperDocument : AuditableEntity
{
    public Guid PaperId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? MediaType { get; set; }
    public string? StoragePath { get; set; }
    public PaperDocumentStatus Status { get; set; } = PaperDocumentStatus.Pending;
    public bool RequiresOcr { get; set; }
    public string? ExtractedText { get; set; }

    /// <summary>
    /// JSON object containing document-specific metadata. Schema varies by source:
    /// <list type="bullet">
    ///   <item><description>Semantic Scholar imports: { semanticScholarId: string, doi?: string, citationCount?: number, ... }</description></item>
    ///   <item><description>arXiv imports: { arxivId: string, categories: string[], ... }</description></item>
    ///   <item><description>Direct uploads: { uploadedBy: string, uploadTimestamp: datetime, ... }</description></item>
    /// </list>
    /// </summary>
    public string? MetadataJson { get; set; }

    public string? LastError { get; set; }
    public DateTimeOffset? DownloadedAt { get; set; }
    public DateTimeOffset? ExtractedAt { get; set; }

    [ForeignKey(nameof(PaperId))]
    public Paper Paper { get; set; } = null!;

    public ICollection<DocumentChunk> Chunks { get; set; } = [];
}
