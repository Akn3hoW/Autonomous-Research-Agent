namespace AutonomousResearchAgent.Application.Chat;

public sealed record ChunkSearchResult(
    Guid PaperId,
    string PaperTitle,
    Guid ChunkId,
    string ChunkText,
    double Score)
{
    public string ToChunkCitation(int position) => $"[source:{ChunkId}:{PaperId}]";
}

public sealed record ChatResult(
    string Answer,
    IReadOnlyList<ChunkSearchResult> Sources,
    IReadOnlyList<ChunkCitation>? StructuredCitations = null);

public sealed record ChunkCitation(
    Guid PaperId,
    Guid ChunkId,
    string ChunkText,
    double Score,
    int Position)
{
    public string ToCitationString() => $"[source:{ChunkId}:{PaperId}]";
}

public sealed record ToolExecutionRequest(
    string ToolName,
    Dictionary<string, object> Parameters);

public sealed record ChatRequestWithTools(
    string Question,
    int TopK = 10,
    bool IncludeTools = false);
