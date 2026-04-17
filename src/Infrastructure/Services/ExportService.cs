using System.Security.Cryptography;
using System.Text;
using AutonomousResearchAgent.Application.Export;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class ExportService : IExportService
{
    public string ToBibtex(Guid paperId, string title, IReadOnlyCollection<string> authors, int? year, string? doi, string? venue, int citationCount)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(authors);

        var citeKey = GenerateCiteKey(title, authors.FirstOrDefault(), year);
        var sb = new StringBuilder();
        sb.AppendLine($"@article{{{citeKey},");
        sb.AppendLine($"  title = {{{EscapeLatex(title)}}},");

        if (authors.Count > 0)
        {
            var authorsStr = string.Join(" and ", authors.Select(a => FormatAuthor(a)));
            sb.AppendLine($"  author = {{{authorsStr}}},");
        }

        if (year.HasValue)
            sb.AppendLine($"  year = {{{year.Value}}},");

        if (!string.IsNullOrWhiteSpace(doi))
            sb.AppendLine($"  doi = {{{doi}}},");

        if (!string.IsNullOrWhiteSpace(venue))
            sb.AppendLine($"  journal = {{{EscapeLatex(venue)}}},");

        sb.AppendLine($"  citation_count = {{{citationCount}}},");
        sb.AppendLine($"  url = {{https://arxiv.org/abs/{paperId}}}");
        sb.Append('}');
        return sb.ToString();
    }

    public string ToRis(Guid paperId, string title, IReadOnlyCollection<string> authors, int? year, string? doi, string? venue, int citationCount)
    {
        ArgumentNullException.ThrowIfNull(authors);

        var sb = new StringBuilder();
        sb.AppendLine("TY  - JOUR");
        sb.AppendLine($"TI  - {title}");

        foreach (var author in authors)
        {
            sb.AppendLine($"AU  - {author}");
        }

        if (year.HasValue)
            sb.AppendLine($"PY  - {year.Value}");

        if (!string.IsNullOrWhiteSpace(doi))
            sb.AppendLine($"DO  - {doi}");

        if (!string.IsNullOrWhiteSpace(venue))
            sb.AppendLine($"JO  - {venue}");

        sb.AppendLine($"ER  - ");
        return sb.ToString();
    }

    private static string GenerateCiteKey(string title, string? firstAuthor, int? year)
    {
        var key = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(firstAuthor))
        {
            var lastName = firstAuthor.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "unknown";
            key.Append(SanitizeCiteKey(lastName));
        }
        else
        {
            var firstWord = title.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "paper";
            key.Append(SanitizeCiteKey(firstWord));
        }

        if (year.HasValue)
            key.Append(year.Value);

        return key.ToString().ToLowerInvariant();
    }

    private static string SanitizeCiteKey(string key)
    {
        return new string(key.Where(c => char.IsLetterOrDigit(c)).ToArray());
    }

    private static string FormatAuthor(string author)
    {
        var parts = author.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var lastName = parts.Last();
            var firstNames = string.Join(" ", parts[..^1]);
            return $"{lastName}, {firstNames}";
        }
        return author;
    }

    private static string EscapeLatex(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        return text
            .Replace("\\", "\\textbackslash{}")
            .Replace("&", "\\&")
            .Replace("%", "\\%")
            .Replace("$", "\\$")
            .Replace("#", "\\#")
            .Replace("_", "\\_")
            .Replace("{", "\\{")
            .Replace("}", "\\}")
            .Replace("~", "\\textasciitilde{}")
            .Replace("^", "\\textasciicircum{}");
    }
}
