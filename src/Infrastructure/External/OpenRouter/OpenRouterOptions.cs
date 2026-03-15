namespace AutonomousResearchAgent.Infrastructure.External.OpenRouter;

public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string Model { get; set; } = "openrouter/hunter-alpha";
    public string? ApiKey { get; set; }
    public string? HttpReferer { get; set; }
    public string AppTitle { get; set; } = "Autonomous Research Agent";
    public int TimeoutSeconds { get; set; } = 120;
}
