using System;
using Microsoft.Extensions.Options;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.External.SemanticScholar;
using AutonomousResearchAgent.Infrastructure.Services;

namespace AutonomousResearchAgent.Infrastructure.Configuration;

public sealed class OpenRouterOptionsValidator : IValidateOptions<OpenRouterOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenRouterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("OpenRouter:ApiKey is required. Set the 'OPENROUTER_API_KEY' environment variable or configure 'OpenRouter:ApiKey' in appsettings.json.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class SemanticScholarOptionsValidator : IValidateOptions<SemanticScholarOptions>
{
    public ValidateOptionsResult Validate(string? name, SemanticScholarOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("SemanticScholar:ApiKey is required. Configure 'SemanticScholar:ApiKey' in appsettings.json.");
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class LocalEmbeddingOptionsValidator : IValidateOptions<LocalEmbeddingOptions>
{
    public ValidateOptionsResult Validate(string? name, LocalEmbeddingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.VectorDimensions != 768)
        {
            return ValidateOptionsResult.Fail($"LocalEmbedding:VectorDimensions must be 768 for Snowflake/snowflake-arctic-embed-m-v1.5 model. Current value: {options.VectorDimensions}");
        }

        return ValidateOptionsResult.Success;
    }
}
