namespace AutonomousResearchAgent.Application.Papers;

public interface IArxivClient
{
    Task<ArxivPaper?> GetPaperAsync(string arxivId, CancellationToken cancellationToken);
    Task<IEnumerable<ArxivPaper>> SearchAsync(string query, CancellationToken cancellationToken);
}

public sealed record ArxivPaper(
    string Id,
    string Title,
    string Summary,
    IReadOnlyCollection<string> Authors,
    DateTimeOffset Published,
    DateTimeOffset Updated,
    IReadOnlyCollection<string> Categories,
    string? PdfUrl,
    string? Doi);
