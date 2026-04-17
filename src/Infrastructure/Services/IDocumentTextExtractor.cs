namespace AutonomousResearchAgent.Infrastructure.Services;

public interface IDocumentTextExtractor
{
    Task<string?> ExtractAsync(byte[] bytes, string? mediaType, string fileName, CancellationToken cancellationToken);
}
