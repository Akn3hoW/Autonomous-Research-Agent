namespace AutonomousResearchAgent.Application.Papers;

public interface ICrossRefClient
{
    Task<CrossRefPaper?> GetByDoiAsync(string doi, CancellationToken cancellationToken);
}

public sealed record CrossRefPaper(
    string Doi,
    string Title,
    string? Abstract,
    IReadOnlyCollection<string> Authors,
    DateTimeOffset? Published,
    string? Publisher,
    IReadOnlyCollection<string> ContainerTitle,
    string? Type);
