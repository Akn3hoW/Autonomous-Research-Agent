using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Application.Search;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string content, EmbeddingType embeddingType, CancellationToken cancellationToken);
    Task<float[]> GenerateQueryEmbeddingAsync(string query, CancellationToken cancellationToken);
}

