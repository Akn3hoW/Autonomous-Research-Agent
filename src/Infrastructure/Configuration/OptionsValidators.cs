using Microsoft.Extensions.Options;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.External.SemanticScholar;

namespace AutonomousResearchAgent.Infrastructure.Configuration;

public sealed class OpenRouterOptionsValidator : IValidateOptions<OpenRouterOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenRouterOptions options)
    {
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
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("SemanticScholar:ApiKey is required. Configure 'SemanticScholar:ApiKey' in appsettings.json.");
        }

        return ValidateOptionsResult.Success;
    }
}
