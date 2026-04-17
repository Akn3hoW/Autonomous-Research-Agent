using System.Text.Json.Nodes;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Documents;

public sealed record PaperDocumentModel(
    Guid Id,
    Guid PaperId,
    string SourceUrl,
    string? FileName,
    string? MediaType,
    string? StoragePath,
    PaperDocumentStatus Status,
    bool RequiresOcr,
    string? ExtractedText,
    JsonNode? Metadata,
    string? LastError,
    DateTimeOffset? DownloadedAt,
    DateTimeOffset? ExtractedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreatePaperDocumentCommand(
    Guid PaperId,
    string SourceUrl,
    string? FileName,
    string? MediaType,
    bool RequiresOcr,
    JsonNode? Metadata);

public sealed record QueuePaperDocumentProcessingCommand(
    string? RequestedBy,
    bool Force = false);
