namespace AutonomousResearchAgent.Application.Export;

public interface IExportService
{
    string ToBibtex(Guid paperId, string title, IReadOnlyCollection<string> authors, int? year, string? doi, string? venue, int citationCount);
    string ToRis(Guid paperId, string title, IReadOnlyCollection<string> authors, int? year, string? doi, string? venue, int citationCount);
}
