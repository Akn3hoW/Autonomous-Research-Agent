namespace AutonomousResearchAgent.Application.Summaries;

public interface ISummaryDiffService
{
    Task<SummaryDiffModel> ComputeDiffAsync(Guid paperId, Guid summaryId1, Guid summaryId2, CancellationToken cancellationToken);
}

public sealed record SummaryDiffModel(
    Guid PaperId,
    string PaperTitle,
    SummaryVersionModel Summary1,
    SummaryVersionModel Summary2,
    FieldDiffsModel FieldDiffs,
    double OverallSimilarity);

public sealed record SummaryVersionModel(
    Guid Id,
    string ModelName,
    string PromptVersion,
    DateTimeOffset CreatedAt,
    string SummaryText,
    string Status);

public sealed record FieldDiffsModel(
    FieldDiffModel Summary,
    FieldDiffModel ModelName,
    FieldDiffModel PromptVersion);

public sealed record FieldDiffModel(
    string Left,
    string Right,
    string? DiffHtml,
    bool Changed);