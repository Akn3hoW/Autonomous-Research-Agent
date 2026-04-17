namespace AutonomousResearchAgent.Infrastructure.Services;

public interface ITextChunkingService
{
    IReadOnlyList<TextChunk> ChunkText(string text, int chunkSize = 512, int overlap = 64);
}

public sealed class TextChunk
{
    public required string Text { get; init; }
    public int Index { get; init; }
    public int StartPosition { get; init; }
    public int EndPosition { get; init; }
}
