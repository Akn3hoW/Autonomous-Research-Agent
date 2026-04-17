namespace AutonomousResearchAgent.Infrastructure.Services;

public interface ILocalEmbeddingClient
{
    Task<float[]> GenerateEmbeddingAsync(string content, CancellationToken cancellationToken);
}
