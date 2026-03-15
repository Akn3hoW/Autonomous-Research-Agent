using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Api.Contracts.Documents;

public sealed record PaperDocumentDto(
    Guid Id,
    Guid PaperId,
    string SourceUrl,
    string? FileName,
    string? MediaType,
    string? StoragePath,
    string Status,
    bool RequiresOcr,
    string? ExtractedText,
    JsonNode? Metadata,
    string? LastError,
    DateTimeOffset? DownloadedAt,
    DateTimeOffset? ExtractedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed class CreatePaperDocumentRequest
{
    public string SourceUrl { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string? MediaType { get; init; }
    public bool RequiresOcr { get; init; }
    public JsonNode? Metadata { get; init; }
}

public sealed class QueuePaperDocumentProcessingRequest
{
    public bool Force { get; init; }
}
