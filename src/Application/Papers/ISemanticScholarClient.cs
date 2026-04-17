namespace AutonomousResearchAgent.Application.Papers;

public interface ISemanticScholarClient
{
    Task<IReadOnlyCollection<SemanticScholarPaperImportModel>> SearchPapersAsync(
        IReadOnlyCollection<string> queries,
        int limit,
        CancellationToken cancellationToken);

    Task<SemanticScholarPaperDetails?> GetPaperDetailsAsync(
        string semanticScholarId,
        CancellationToken cancellationToken);
}

public sealed record SemanticScholarPaperDetails(
    string SemanticScholarId,
    IReadOnlyCollection<SemanticScholarCitation> Citations,
    IReadOnlyCollection<SemanticScholarCitation> References);

public sealed record SemanticScholarCitation(
    string SemanticScholarId,
    string Title,
    int? Year,
    string? Context);

