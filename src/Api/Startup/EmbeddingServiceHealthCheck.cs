using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Domain.Enums;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AutonomousResearchAgent.Api.Startup;

public sealed class EmbeddingServiceHealthCheck : IHealthCheck
{
    private readonly IEmbeddingService _embeddingService;

    public EmbeddingServiceHealthCheck(IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var testEmbedding = await _embeddingService.GenerateEmbeddingAsync("test", EmbeddingType.PaperAbstract, cancellationToken);
            if (testEmbedding.Length > 0)
            {
                return HealthCheckResult.Healthy("Embedding service is healthy.");
            }
            return HealthCheckResult.Unhealthy("Embedding service returned empty vector.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Embedding service health check failed.", ex);
        }
    }
}