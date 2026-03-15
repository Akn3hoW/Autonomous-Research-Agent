using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Services;
using Xunit;

namespace Infrastructure.Tests;

public sealed class QueryHelpersTests
{
    [Fact]
    public void ToILikePattern_wraps_value_in_percent_wildcards()
    {
        var result = QueryHelpers.ToILikePattern("test");
        Assert.Equal("%test%", result);
    }

    [Fact]
    public void ToILikePattern_escapes_percent_and_underscore()
    {
        var result = QueryHelpers.ToILikePattern("test%value_name");
        Assert.Equal("%test\\%value\\_name%", result);
    }

    [Fact]
    public void Truncate_returns_null_for_null_input()
    {
        var result = QueryHelpers.Truncate(null, 100);
        Assert.Null(result);
    }

    [Fact]
    public void Truncate_returns_empty_for_empty_string()
    {
        var result = QueryHelpers.Truncate("", 100);
        Assert.Equal("", result);
    }

    [Fact]
    public void Truncate_returns_original_when_under_max_length()
    {
        var input = "short text";
        var result = QueryHelpers.Truncate(input, 100);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Truncate_truncates_when_over_max_length()
    {
        var input = new string('a', 300);
        var result = QueryHelpers.Truncate(input, 100);
        Assert.Equal(100, result.Length);
        Assert.Equal(new string('a', 100), result);
    }

    [Fact]
    public void FormatPaper_includes_all_fields()
    {
        var paper = new Paper
        {
            Title = "Test Title",
            Authors = ["John Doe", "Jane Smith"],
            Year = 2024,
            Venue = "Test Conference",
            Abstract = "Test abstract",
            Documents =
            [
                new PaperDocument { ExtractedText = new string('x', 3000) }
            ],
            Summaries =
            [
                new PaperSummary { SearchText = "Test summary", CreatedAt = DateTime.UtcNow }
            ]
        };

        var result = QueryHelpers.FormatPaper(paper);

        Assert.Contains("Title: Test Title", result);
        Assert.Contains("Authors: John Doe, Jane Smith", result);
        Assert.Contains("Year: 2024", result);
        Assert.Contains("Venue: Test Conference", result);
        Assert.Contains("Abstract: Test abstract", result);
        Assert.Contains("Summary: Test summary", result);
        Assert.Contains("ExtractedText:", result);
    }

    [Fact]
    public void FormatPaper_truncates_extracted_text_to_2500_chars()
    {
        var paper = new Paper
        {
            Title = "Test",
            Authors = [],
            Documents =
            [
                new PaperDocument { ExtractedText = new string('x', 3000) }
            ]
        };

        var result = QueryHelpers.FormatPaper(paper);

        Assert.Contains($"ExtractedText: {new string('x', 2500)}", result);
    }

    [Fact]
    public void FormatPaper_handles_paper_with_no_documents()
    {
        var paper = new Paper
        {
            Title = "Test",
            Authors = ["Author"]
        };

        var result = QueryHelpers.FormatPaper(paper);

        Assert.Contains("ExtractedText:", result);
        Assert.DoesNotContain("ExtractedText: null", result);
    }

    [Fact]
    public void FormatPapers_formats_multiple_papers()
    {
        var papers = new[]
        {
            new Paper { Title = "Paper 1", Authors = ["Author 1"] },
            new Paper { Title = "Paper 2", Authors = ["Author 2"] }
        };

        var result = QueryHelpers.FormatPapers(papers);

        Assert.Contains("Paper 1", result);
        Assert.Contains("Paper 2", result);
    }

    [Fact]
    public void FormatPaper_uses_latest_summary()
    {
        var olderSummary = new PaperSummary { SearchText = "Old summary", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        var newerSummary = new PaperSummary { SearchText = "New summary", CreatedAt = DateTime.UtcNow };

        var paper = new Paper
        {
            Title = "Test",
            Authors = [],
            Summaries = [olderSummary, newerSummary]
        };

        var result = QueryHelpers.FormatPaper(paper);

        Assert.Contains("Summary: New summary", result);
    }
}
