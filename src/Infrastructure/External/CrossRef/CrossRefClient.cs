using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Papers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.External.CrossRef;

public sealed class CrossRefOptions
{
    public const string SectionName = "CrossRef";
    public string BaseUrl { get; set; } = "https://api.crossref.org";
    public string? UserAgent { get; set; } = "AutonomousResearchAgent/1.0";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class CrossRefClient(
    HttpClient httpClient,
    IOptions<CrossRefOptions> options,
    ILogger<CrossRefClient> logger) : ICrossRefClient
{
    private readonly CrossRefOptions _options = options.Value;

    public async Task<CrossRefPaper?> GetByDoiAsync(string doi, CancellationToken cancellationToken)
    {
        var requestUri = $"/works/{Uri.EscapeDataString(doi)}";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            request.Headers.Add("User-Agent", _options.UserAgent);

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CrossRef request failed with {StatusCode} for DOI {Doi}", response.StatusCode, doi);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<CrossRefResponse>(cancellationToken: cancellationToken);
            if (payload?.Message == null)
                return null;

            var msg = payload.Message;
            var title = msg.Title?.FirstOrDefault() ?? "";
            var authors = msg.Author?.Select(a => $"{a.Given ?? ""} {a.Family ?? ""}".Trim()).Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? [];
            var published = msg.Published?.DateTime;
            var containerTitles = msg.ContainerTitle ?? [];
            var publisher = msg.Publisher;
            var type = msg.Type;

            return new CrossRefPaper(
                doi,
                title,
                msg.Abstract,
                authors,
                published,
                publisher,
                containerTitles,
                type);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "CrossRef request failed for DOI {Doi}", doi);
            throw new ExternalDependencyException("CrossRef request failed.", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex, "CrossRef response parsing failed for DOI {Doi}", doi);
            throw new ExternalDependencyException("CrossRef returned an unexpected payload.", ex);
        }
    }

    internal sealed record CrossRefResponse(
        [property: JsonPropertyName("message")] CrossRefMessage? Message);

    internal sealed record CrossRefMessage(
        [property: JsonPropertyName("DOI")] string Doi,
        [property: JsonPropertyName("title")] List<string>? Title,
        [property: JsonPropertyName("abstract")] string? Abstract,
        [property: JsonPropertyName("author")] List<CrossRefAuthor>? Author,
        [property: JsonPropertyName("published")] CrossRefPublished? Published,
        [property: JsonPropertyName("publisher")] string? Publisher,
        [property: JsonPropertyName("container-title")] List<string>? ContainerTitle,
        [property: JsonPropertyName("type")] string? Type);

    internal sealed record CrossRefAuthor(
        [property: JsonPropertyName("given")] string? Given,
        [property: JsonPropertyName("family")] string? Family);

    internal sealed record CrossRefPublished(
        [property: JsonPropertyName("date-time")] DateTimeOffset? DateTime);
}
