using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class SummaryDiffService(ApplicationDbContext dbContext) : ISummaryDiffService
{
    public async Task<SummaryDiffModel> ComputeDiffAsync(Guid paperId, Guid summaryId1, Guid summaryId2, CancellationToken cancellationToken)
    {
        var paper = await dbContext.Papers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paperId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Paper), paperId);

        var summary1 = await dbContext.PaperSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == summaryId1 && s.PaperId == paperId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.PaperSummary), summaryId1);

        var summary2 = await dbContext.PaperSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == summaryId2 && s.PaperId == paperId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.PaperSummary), summaryId2);

        var summary1Text = summary1.SummaryJson != null ? ExtractSummaryText(summary1.SummaryJson) : summary1.SearchText ?? "";
        var summary2Text = summary2.SummaryJson != null ? ExtractSummaryText(summary2.SummaryJson) : summary2.SearchText ?? "";

        var overallSimilarity = ComputeSimilarity(summary1Text, summary2Text);

        return new SummaryDiffModel(
            paperId,
            paper.Title,
            ToVersionModel(summary1),
            ToVersionModel(summary2),
            new FieldDiffsModel(
                new FieldDiffModel(summary1Text, summary2Text, ComputeDiffHtml(summary1Text, summary2Text), summary1Text != summary2Text),
                new FieldDiffModel(summary1.ModelName, summary2.ModelName, null, summary1.ModelName != summary2.ModelName),
                new FieldDiffModel(summary1.PromptVersion, summary2.PromptVersion, null, summary1.PromptVersion != summary2.PromptVersion)),
            overallSimilarity);
    }

    private static SummaryVersionModel ToVersionModel(Domain.Entities.PaperSummary summary)
    {
        return new SummaryVersionModel(
            summary.Id,
            summary.ModelName,
            summary.PromptVersion,
            summary.CreatedAt,
            summary.SummaryJson != null ? ExtractSummaryText(summary.SummaryJson) : summary.SearchText ?? "",
            summary.Status.ToString());
    }

    private static string ExtractSummaryText(string json)
    {
        try
        {
            var node = JsonNode.Parse(json);
            var shortSummary = node?["shortSummary"]?.GetValue<string>();
            var longSummary = node?["longSummary"]?.GetValue<string>();
            var keyFindings = node?["keyFindings"]?.AsArray()?.Select(x => x?.GetValue<string>()).Where(x => x != null).ToList() ?? [];

            var parts = new[] { shortSummary, longSummary }.Where(x => !string.IsNullOrWhiteSpace(x))
                .Concat(keyFindings.Where(x => !string.IsNullOrWhiteSpace(x)))
                .ToList();

            return string.Join(" ", parts);
        }
        catch
        {
            return json;
        }
    }

    private static double ComputeSimilarity(string text1, string text2)
    {
        if (string.IsNullOrWhiteSpace(text1) && string.IsNullOrWhiteSpace(text2))
            return 1.0;
        if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
            return 0.0;

        var words1 = text1.ToLowerInvariant().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        var words2 = text2.ToLowerInvariant().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        var set1 = new HashSet<string>(words1);
        var set2 = new HashSet<string>(words2);
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private static string ComputeDiffHtml(string text1, string text2)
    {
        if (text1 == text2)
            return "";

        var words1 = text1.Split(' ');
        var words2 = text2.Split(' ');
        var diff = new List<string>();

        int i = 0, j = 0;
        while (i < words1.Length || j < words2.Length)
        {
            if (i >= words1.Length)
            {
                diff.Add($"<ins>{string.Join(" ", words2.Skip(j))}</ins>");
                break;
            }
            if (j >= words2.Length)
            {
                diff.Add($"<del>{string.Join(" ", words1.Skip(i))}</del>");
                break;
            }
            if (words1[i] == words2[j])
            {
                diff.Add(words1[i]);
                i++;
                j++;
            }
            else
            {
                diff.Add($"<del>{words1[i]}</del><ins>{words2[j]}</ins>");
                i++;
                j++;
            }
        }

        return string.Join(" ", diff.Take(100));
    }
}
