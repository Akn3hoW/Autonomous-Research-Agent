namespace AutonomousResearchAgent.Application.Papers;

public interface ISemanticScholarClient
{
    Task<IReadOnlyCollection<SemanticScholarPaperImportModel>> SearchPapersAsync(
        IReadOnlyCollection<string> queries,
        int limit,
        CancellationToken cancellationToken);
}

