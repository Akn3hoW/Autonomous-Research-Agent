using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class VisionPdfExtractor : IDocumentTextExtractor
{
    private readonly VisionPdfExtractorOptions _options;
    private readonly ILogger<VisionPdfExtractor> _logger;
    private readonly HttpClient _httpClient;

    public VisionPdfExtractor(
        IOptions<VisionPdfExtractorOptions> options,
        ILogger<VisionPdfExtractor> logger,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<string?> ExtractAsync(byte[] bytes, string? mediaType, string fileName, CancellationToken cancellationToken)
    {
        if (!IsVisionFallbackEnabled())
        {
            _logger.LogDebug("Vision fallback is disabled");
            return null;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension != ".pdf" && !string.Equals(mediaType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var base64Content = Convert.ToBase64String(bytes);
            var extractedText = await CallVisionApiAsync(base64Content, cancellationToken);
            return extractedText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vision-based PDF extraction failed for {FileName}", fileName);
            return null;
        }
    }

    private bool IsVisionFallbackEnabled()
    {
        return _options.UseVisionFallback &&
               !string.IsNullOrWhiteSpace(_options.VisionApiKey);
    }

    private async Task<string> CallVisionApiAsync(string base64Content, CancellationToken cancellationToken)
    {
        var model = _options.VisionModel?.ToLowerInvariant() switch
        {
            "gpt-4o" => "gpt-4o",
            _ => "gemini-1.5-flash"
        };

        if (model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase))
        {
            return await CallGeminiApiAsync(base64Content, cancellationToken);
        }

        return await CallOpenAiVisionApiAsync(base64Content, cancellationToken);
    }

    private async Task<string> CallGeminiApiAsync(string base64Content, CancellationToken cancellationToken)
    {
        using var requestContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                contents = new object[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "Extract all text content from this PDF document. Return only the text, preserving paragraph structure and formatting." },
                            new { inline_data = new { mime_type = "application/pdf", data = base64Content } }
                        }
                    }
                }
            }),
            Encoding.UTF32,
            "application/json");

        var response = await _httpClient.PostAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{_options.VisionModel}:generateContent?key={_options.VisionApiKey}",
            requestContent,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        using var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(cancellationToken: cancellationToken);

        return json?.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> CallOpenAiVisionApiAsync(string base64Content, CancellationToken cancellationToken)
    {
        using var requestContent = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                model = _options.VisionModel,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = "Extract all text content from this PDF document. Return only the text, preserving paragraph structure and formatting." },
                            new { type = "image_url", image_url = new { url = $"data:application/pdf;base64,{base64Content}" } }
                        }
                    }
                },
                max_tokens = 4096
            }),
            Encoding.UTF32,
            "application/json");

        var response = await _httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            requestContent,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        using var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>(cancellationToken: cancellationToken);

        return json?.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}

public sealed class VisionPdfExtractorOptions
{
    public const string SectionName = "VisionPdfExtractor";

    public bool UseVisionFallback { get; set; } = false;

    public string VisionModel { get; set; } = "gemini-1.5-flash";

    public string? VisionApiKey { get; set; }
}