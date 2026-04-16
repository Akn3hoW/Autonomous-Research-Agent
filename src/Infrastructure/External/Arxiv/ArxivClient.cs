using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Papers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.External.Arxiv;

public sealed class ArxivOptions
{
    public const string SectionName = "Arxiv";
    public string BaseUrl { get; set; } = "http://export.arxiv.org/api";
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class ArxivClient(
    HttpClient httpClient,
    IOptions<ArxivOptions> options,
    ILogger<ArxivClient> logger) : IArxivClient
{
    private readonly ArxivOptions _options = options.Value;

    public async Task<ArxivPaper?> GetPaperAsync(string arxivId, CancellationToken cancellationToken)
    {
        var requestUri = $"?id_list={Uri.EscapeDataString(arxivId)}";
        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseArxivResponse(content, arxivId)?.FirstOrDefault();
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Arxiv request failed for ID {ArxivId}", arxivId);
            throw new ExternalDependencyException("Arxiv request failed.", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex, "Arxiv response parsing failed for ID {ArxivId}", arxivId);
            throw new ExternalDependencyException("Arxiv returned an unexpected payload.", ex);
        }
    }

    public async Task<IEnumerable<ArxivPaper>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var requestUri = $"?search_query={Uri.EscapeDataString(query)}&start=0&max_results=50&sortBy=relevance";
        try
        {
            using var response = await httpClient.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseArxivResponse(content, null) ?? [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Arxiv search request failed for query {Query}", query);
            throw new ExternalDependencyException("Arxiv search request failed.", ex);
        }
        catch (System.Text.Json.JsonException ex)
        {
            logger.LogError(ex, "Arxiv search response parsing failed for query {Query}", query);
            throw new ExternalDependencyException("Arxiv returned an unexpected payload.", ex);
        }
    }

    private List<ArxivPaper>? ParseArxivResponse(string content, string? filterId)
    {
        var papers = new List<ArxivPaper>();
        var doc = System.Xml.Linq.XDocument.Parse(content);
        var entries = doc.Descendants("entry");

        foreach (var entry in entries)
        {
            var id = entry.Element("id")?.Value ?? "";
            if (filterId != null && !id.Contains(filterId, StringComparison.OrdinalIgnoreCase))
                continue;

            var title = entry.Element("title")?.Value?.Replace("\n", " ").Trim() ?? "";
            var summary = entry.Element("summary")?.Value?.Replace("\n", " ").Trim() ?? "";
            var authors = entry.Elements("author").Select(a => a.Element("name")?.Value ?? "").Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            var published = DateTimeOffset.TryParse(entry.Element("published")?.Value, out var pub) ? pub : DateTimeOffset.MinValue;
            var updated = DateTimeOffset.TryParse(entry.Element("updated")?.Value, out var upd) ? upd : published;
            var categories = entry.Elements("category").Select(c => c.Attribute("term")?.Value ?? "").Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

            var pdfLink = entry.Elements("link").FirstOrDefault(l => l.Attribute("type")?.Value == "application/pdf")?.Attribute("href")?.Value;
            var doi = entry.Element("arxiv:doi")?.Value ?? entry.Elements("identifier").FirstOrDefault()?.Value;

            papers.Add(new ArxivPaper(
                ExtractArxivId(id),
                title,
                summary,
                authors,
                published,
                updated,
                categories,
                pdfLink,
                doi));
        }

        return papers.Count > 0 ? papers : null;
    }

    private static string ExtractArxivId(string url)
    {
        var parts = url.Split('/');
        return parts.Length > 0 ? parts[^1] : url;
    }
}
