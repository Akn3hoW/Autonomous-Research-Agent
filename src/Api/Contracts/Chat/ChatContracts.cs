using System.ComponentModel.DataAnnotations;

namespace AutonomousResearchAgent.Api.Contracts.Chat;

public sealed class ChatRequest
{
    public string Question { get; init; } = string.Empty;

    [Range(1, 100)]
    public int TopK { get; init; } = 10;
}

public sealed class ChatRequestWithToolsRequest
{
    public string Question { get; init; } = string.Empty;

    [Range(1, 100)]
    public int TopK { get; init; } = 10;
    public bool IncludeTools { get; init; } = false;
}

public sealed record ChatSourceDto(
    Guid PaperId,
    string PaperTitle,
    Guid ChunkId,
    string ChunkText,
    double RelevanceScore);

public sealed record SourceDetailDto(
    Guid PaperId,
    Guid ChunkId,
    string ChunkText,
    double Score,
    int Position,
    string PaperTitle);

public sealed record ChatResponseDto(
    string Content,
    ChatSourceDto[] Sources);

public sealed record ChatMessageDto(
    string Role,
    string Content,
    ChatSourceDto[]? Sources);
