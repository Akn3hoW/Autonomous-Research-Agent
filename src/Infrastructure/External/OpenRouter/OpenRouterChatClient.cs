using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.External.OpenRouter;

public sealed class OpenRouterChatClient(
    HttpClient httpClient,
    IOptions<OpenRouterOptions> options,
    ILogger<OpenRouterChatClient> logger)
{
    private readonly OpenRouterOptions _options = options.Value;

    public async Task<JsonNode?> CreateJsonCompletionAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken,
        double temperature = 0.2)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenRouter API key is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(new
            {
                model = _options.Model,
                temperature,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            })
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add("X-Title", _options.AppTitle);
        if (!string.IsNullOrWhiteSpace(_options.HttpReferer))
        {
            request.Headers.Referrer = new Uri(_options.HttpReferer);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("OpenRouter returned {StatusCode}. Body: {Body}", response.StatusCode, payload);
                response.EnsureSuccessStatusCode();
            }

            using var document = JsonDocument.Parse(payload);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ExternalDependencyException("OpenRouter returned an empty completion payload.");
            }

            return JsonNode.Parse(ExtractJson(content));
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "OpenRouter request failed.");
            throw new ExternalDependencyException("OpenRouter request failed.", ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "OpenRouter returned an invalid JSON payload.");
            throw new ExternalDependencyException("OpenRouter returned an invalid JSON payload.", ex);
        }
    }

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var lines = trimmed.Split('\n').ToList();
            if (lines.Count >= 2)
            {
                lines.RemoveAt(0);
                if (lines.Count > 0 && lines[^1].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    lines.RemoveAt(lines.Count - 1);
                }

                trimmed = string.Join("\n", lines).Trim();
            }
        }

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return trimmed[firstBrace..(lastBrace + 1)];
        }

        return trimmed;
    }
}
