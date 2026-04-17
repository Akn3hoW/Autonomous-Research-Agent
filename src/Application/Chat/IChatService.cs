namespace AutonomousResearchAgent.Application.Chat;

public interface IChatService
{
    IAsyncEnumerable<string> StreamChatAsync(string question, int topK, CancellationToken cancellationToken);
    IAsyncEnumerable<string> StreamChatWithToolsAsync(string question, int topK, bool includeTools, CancellationToken cancellationToken);
    Task<ChatResult> ChatAsync(string question, int topK, CancellationToken cancellationToken);
    Task<ChatResult> ChatWithToolsAsync(ChatRequestWithTools request, CancellationToken cancellationToken);
    Task<ChunkCitation?> GetSourceAsync(Guid chunkId, Guid paperId, CancellationToken cancellationToken);
}
