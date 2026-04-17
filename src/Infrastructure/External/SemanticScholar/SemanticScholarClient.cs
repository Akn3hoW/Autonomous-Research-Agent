using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Papers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.External.SemanticScholar;

public sealed class SemanticScholarClient(
    HttpClient httpClient,
    IOptions<SemanticScholarOptions> options,
    ILogger<SemanticScholarClient> logger) : ISemanticScholarClient
{
    private readonly SemanticScholarOptions _options = options.Value;

    public async Task<IReadOnlyCollection<SemanticScholarPaperImportModel>> SearchPapersAsync(
        IReadOnlyCollection<string> queries,
        int limit,
        CancellationToken cancellationToken)
    {
        var results = new List<SemanticScholarPaperImportModel>();

        foreach (var query in queries.Where(q => !string.IsNullOrWhiteSpace(q)))
        {
            var requestUri = $"/graph/v1/paper/search?query={Uri.EscapeDataString(query)}&limit={limit}&fields={Uri.EscapeDataString(_options.Fields)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Add("x-api-key", _options.ApiKey);
            }

            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<SemanticScholarSearchResponse>(cancellationToken: cancellationToken);
                if (payload?.Data is null)
                {
                    continue;
                }

                results.AddRange(payload.Data.Select(Map));
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "Semantic Scholar request failed for query {Query}", query);
                throw new ExternalDependencyException("Semantic Scholar request failed.", ex);
            }
            catch (System.Text.Json.JsonException ex)
            {
                logger.LogError(ex, "Semantic Scholar response parsing failed for query {Query}", query);
                throw new ExternalDependencyException("Semantic Scholar returned an unexpected payload.", ex);
            }
            catch (NotSupportedException ex)
            {
                logger.LogError(ex, "Semantic Scholar response content type was not supported for query {Query}", query);
                throw new ExternalDependencyException("Semantic Scholar returned an unsupported response payload.", ex);
            }
        }

        return results
            .GroupBy(x => x.SemanticScholarId)
            .Select(group => group.First())
            .ToList();
    }

    public async Task<SemanticScholarPaperDetails?> GetPaperDetailsAsync(
        string semanticScholarId,
        CancellationToken cancellationToken)
    {
        var requestUri = $"/graph/v1/paper/{Uri.EscapeDataString(semanticScholarId)}?fields={Uri.EscapeDataString(_options.Fields)},citations.title,citations.year,citations.externalIds,citations.contexts,references.title,references.year,references.externalIds,references.contexts";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Add("x-api-key", _options.ApiKey);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Semantic Scholar paper details request failed with {StatusCode} for {PaperId}", response.StatusCode, semanticScholarId);
                return null;
            }

            var paper = await response.Content.ReadFromJsonAsync<SemanticScholarPaperWithCitations>(cancellationToken: cancellationToken);
            if (paper is null)
            {
                return null;
            }

            var citations = paper.Citations?
                .Where(c => !string.IsNullOrWhiteSpace(c.PaperId))
                .Select(c => new SemanticScholarCitation(
                    c.PaperId!,
                    c.Title ?? "Untitled paper",
                    c.Year,
                    c.Contexts?.FirstOrDefault()))
                .ToList() ?? [];

            var references = paper.References?
                .Where(r => !string.IsNullOrWhiteSpace(r.PaperId))
                .Select(r => new SemanticScholarCitation(
                    r.PaperId!,
                    r.Title ?? "Untitled paper",
                    r.Year,
                    r.Contexts?.FirstOrDefault()))
                .ToList() ?? [];

            return new SemanticScholarPaperDetails(
                semanticScholarId,
                citations,
                references);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Semantic Scholar paper details request failed for {PaperId}", semanticScholarId);
            throw new ExternalDependencyException("Semantic Scholar request failed.", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex, "Semantic Scholar paper details response parsing failed for {PaperId}", semanticScholarId);
            throw new ExternalDependencyException("Semantic Scholar returned an unexpected payload.", ex);
        }
    }

    private static SemanticScholarPaperImportModel Map(SemanticScholarPaper paper)
    {
        return new SemanticScholarPaperImportModel(
            paper.PaperId ?? Guid.NewGuid().ToString("N"),
            paper.ExternalIds?.Doi,
            paper.Title ?? "Untitled paper",
            paper.Abstract,
            paper.Authors?.Select(a => a.Name ?? string.Empty).Where(a => !string.IsNullOrWhiteSpace(a)).ToList() ?? [],
            paper.Year,
            paper.Venue,
            paper.CitationCount ?? 0,
            new JsonObject
            {
                ["source"] = "SemanticScholar",
                ["rawPaperId"] = paper.PaperId,
                ["openAccessPdfUrl"] = paper.OpenAccessPdf?.Url
            });
    }

    internal sealed record SemanticScholarSearchResponse(
        [property: JsonPropertyName("data")] List<SemanticScholarPaper>? Data);

    internal sealed record SemanticScholarPaper(
        [property: JsonPropertyName("paperId")] string? PaperId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("abstract")] string? Abstract,
        [property: JsonPropertyName("authors")] List<SemanticScholarAuthor>? Authors,
        [property: JsonPropertyName("year")] int? Year,
        [property: JsonPropertyName("venue")] string? Venue,
        [property: JsonPropertyName("citationCount")] int? CitationCount,
        [property: JsonPropertyName("externalIds")] SemanticScholarExternalIds? ExternalIds,
        [property: JsonPropertyName("openAccessPdf")] SemanticScholarPdf? OpenAccessPdf);

    internal sealed record SemanticScholarAuthor([property: JsonPropertyName("name")] string? Name);

    internal sealed record SemanticScholarExternalIds([property: JsonPropertyName("DOI")] string? Doi);

    internal sealed record SemanticScholarPdf([property: JsonPropertyName("url")] string? Url);

    internal sealed record SemanticScholarPaperWithCitations(
        [property: JsonPropertyName("paperId")] string? PaperId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("year")] int? Year,
        [property: JsonPropertyName("citationCount")] int? CitationCount,
        [property: JsonPropertyName("citations")] List<SemanticScholarCitationPaper>? Citations,
        [property: JsonPropertyName("references")] List<SemanticScholarCitationPaper>? References);

    internal sealed record SemanticScholarCitationPaper(
        [property: JsonPropertyName("paperId")] string? PaperId,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("year")] int? Year,
        [property: JsonPropertyName("externalIds")] SemanticScholarExternalIds? ExternalIds,
        [property: JsonPropertyName("contexts")] List<string>? Contexts);
}
