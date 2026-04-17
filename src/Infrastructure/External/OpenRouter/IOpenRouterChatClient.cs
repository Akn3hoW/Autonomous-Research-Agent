using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutonomousResearchAgent.Infrastructure.External.OpenRouter;

public interface IOpenRouterChatClient
{
    Task<JsonNode?> CreateJsonCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken,
        double temperature = 0.2);

    Task<JsonElement?> CreateCompletionWithToolsAsync(
        string systemPrompt,
        string userPrompt,
        object?[]? tools,
        CancellationToken cancellationToken);
}
