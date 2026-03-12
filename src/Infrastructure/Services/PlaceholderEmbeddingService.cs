using System.Security.Cryptography;
using System.Text;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class PlaceholderEmbeddingService(ILogger<PlaceholderEmbeddingService> logger) : IEmbeddingService
{
    private const int Dimensions = 1536;

    public Task<float[]> GenerateEmbeddingAsync(string content, EmbeddingType embeddingType, CancellationToken cancellationToken)
    {
        logger.LogDebug("Generating placeholder embedding for type {EmbeddingType}", embeddingType);
        return Task.FromResult(CreateDeterministicVector($"{embeddingType}:{content}"));
    }

    public Task<float[]> GenerateQueryEmbeddingAsync(string query, CancellationToken cancellationToken)
    {
        logger.LogDebug("Generating placeholder query embedding");
        return Task.FromResult(CreateDeterministicVector($"query:{query}"));
    }

    private static float[] CreateDeterministicVector(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var vector = new float[Dimensions];

        for (var i = 0; i < Dimensions; i++)
        {
            var source = hash[i % hash.Length];
            vector[i] = (source / 255f) * 2f - 1f;
        }

        return vector;
    }
}

