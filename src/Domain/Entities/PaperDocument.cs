using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

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
    public string? MetadataJson { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? DownloadedAt { get; set; }
    public DateTimeOffset? ExtractedAt { get; set; }

    public Paper Paper { get; set; } = null!;
}
