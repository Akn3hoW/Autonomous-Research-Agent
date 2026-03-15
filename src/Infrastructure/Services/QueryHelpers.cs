using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services;

public static class QueryHelpers
{
    public static async Task<List<Paper>> QueryPapersForFilterAsync(
        IQueryable<Paper> papers,
        string filter,
        int limit,
        CancellationToken cancellationToken)
    {
        var pattern = ToILikePattern(filter);
        return await papers
            .AsNoTracking()
            .Include(p => p.Documents)
            .Include(p => p.Summaries)
            .Where(p => EF.Functions.ILike(p.Title, pattern)
                || (p.Abstract != null && EF.Functions.ILike(p.Abstract, pattern))
                || p.Summaries.Any(s => s.SearchText != null && EF.Functions.ILike(s.SearchText, pattern))
                || p.Documents.Any(d => d.ExtractedText != null && EF.Functions.ILike(d.ExtractedText, pattern)))
            .OrderByDescending(p => p.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public static string ToILikePattern(string value)
    {
        var escaped = value.Trim().Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }

    public static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public static string FormatPaper(Paper paper)
    {
        var extracted = paper.Documents
            .Where(d => !string.IsNullOrWhiteSpace(d.ExtractedText))
            .OrderByDescending(d => d.ExtractedText!.Length)
            .Select(d => d.ExtractedText!)
            .FirstOrDefault();
        var summary = paper.Summaries.OrderByDescending(s => s.CreatedAt).FirstOrDefault()?.SearchText;

        return $"""
Title: {paper.Title}
Authors: {string.Join(", ", paper.Authors)}
Year: {paper.Year}
Venue: {paper.Venue}
Abstract: {paper.Abstract}
Summary: {summary}
ExtractedText: {Truncate(extracted, 2500)}
""";
    }

    public static string FormatPapers(IEnumerable<Paper> papers) =>
        string.Join("\n\n", papers.Select(FormatPaper));

}
