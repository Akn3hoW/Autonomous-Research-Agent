using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.LiteratureReviews;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class LiteratureReviewService(
    ApplicationDbContext dbContext,
    OpenRouterChatClient openRouterChatClient,
    ILogger<LiteratureReviewService> logger) : ILiteratureReviewService
{
    public async Task<LiteratureReviewDetail> CreateAsync(CreateLiteratureReviewCommand command, int userId, CancellationToken cancellationToken)
    {
        var entity = new LiteratureReview
        {
            UserId = userId,
            Title = command.Title,
            ResearchQuestion = command.ResearchQuestion,
            PaperIds = command.PaperIds.ToList(),
            Status = LiteratureReviewStatus.Draft
        };

        dbContext.LiteratureReviews.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var contentJson = await GenerateReviewContentAsync(entity.Id, cancellationToken);
        var sections = ParseSections(contentJson);

        return new LiteratureReviewDetail(
            entity.Id,
            entity.UserId,
            entity.Title,
            entity.ResearchQuestion,
            sections,
            entity.ContentMarkdown,
            entity.Status,
            entity.PaperIds,
            entity.CreatedAt,
            entity.CompletedAt);
    }

    public async Task<LiteratureReviewDetail?> GetByIdAsync(Guid id, int userId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.LiteratureReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        var sections = ParseSections(entity.ContentJson);

        return new LiteratureReviewDetail(
            entity.Id,
            entity.UserId,
            entity.Title,
            entity.ResearchQuestion,
            (IReadOnlyList<LiteratureReviewSection>)sections,
            entity.ContentMarkdown,
            entity.Status,
            entity.PaperIds,
            entity.CreatedAt,
            entity.CompletedAt);
    }

    public async Task<IReadOnlyList<LiteratureReviewModel>> ListAsync(int userId, CancellationToken cancellationToken)
    {
        var items = await dbContext.LiteratureReviews
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return items.Select(r => new LiteratureReviewModel(
            r.Id,
            r.UserId,
            r.Title,
            r.ResearchQuestion,
            r.ContentJson,
            r.ContentMarkdown,
            r.PaperIds,
            r.Status,
            r.CreatedAt,
            r.CompletedAt)).ToList();
    }

    public async Task<string> GenerateReviewContentAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.LiteratureReviews.FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiteratureReview), reviewId);

        entity.Status = LiteratureReviewStatus.Generating;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var papers = await dbContext.Papers
                .AsNoTracking()
                .Where(p => entity.PaperIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            var summaries = await dbContext.PaperSummaries
                .AsNoTracking()
                .Where(s => entity.PaperIds.Contains(s.PaperId) && s.Status == SummaryStatus.Approved)
                .ToListAsync(cancellationToken);

            var paperData = papers.Select(p => new
            {
                id = p.Id,
                title = p.Title,
                authors = p.Authors,
                year = p.Year,
                venue = p.Venue,
                abstractText = p.Abstract,
                summary = summaries.FirstOrDefault(s => s.PaperId == p.Id)?.SummaryJson
            }).ToList();

            var systemPrompt = """
You are an expert academic researcher specializing in systematic literature reviews.
Return valid JSON only.
Generate a comprehensive literature review structured as follows:
{
  "sections": [
    {
      "heading": "Introduction",
      "content": "detailed introduction paragraph(s)",
      "citedPaperIds": [list of paper IDs cited in this section]
    },
    {
      "heading": "Key Themes and Findings",
      "content": "thematic analysis of the literature",
      "citedPaperIds": [list of paper IDs cited]
    },
    {
      "heading": "Research Gaps",
      "content": "identification of gaps and limitations in current research",
      "citedPaperIds": [list of paper IDs cited]
    },
    {
      "heading": "Conclusion and Future Directions",
      "content": "summary and future research directions",
      "citedPaperIds": [list of paper IDs cited]
    }
  ]
}
Cite papers by their numeric ID when making claims.
""";

            var userPrompt = $"""
Research Question: {entity.ResearchQuestion}

Papers to review:
{JsonSerializer.Serialize(paperData, new JsonSerializerOptions { WriteIndented = true })}
""";

            logger.LogInformation("Generating literature review {ReviewId}", reviewId);
            var jsonResponse = await openRouterChatClient.CreateJsonCompletionAsync(systemPrompt, userPrompt, cancellationToken);

            if (jsonResponse is null)
            {
                throw new ExternalDependencyException("Failed to generate literature review content.");
            }

            var contentJson = jsonResponse.ToJsonString();
            var contentMarkdown = RenderToMarkdown(entity.Title, entity.ResearchQuestion, jsonResponse);

            entity.ContentJson = contentJson;
            entity.ContentMarkdown = contentMarkdown;
            entity.Status = LiteratureReviewStatus.Completed;
            entity.CompletedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Completed literature review {ReviewId}", reviewId);

            return contentJson;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate literature review {ReviewId}", reviewId);
            entity.Status = LiteratureReviewStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<string> ExportToMarkdownAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.LiteratureReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiteratureReview), reviewId);

        if (string.IsNullOrEmpty(entity.ContentMarkdown))
        {
            throw new InvalidStateException("Literature review content has not been generated.");
        }

        return entity.ContentMarkdown;
    }

    public async Task<byte[]> ExportToPdfAsync(Guid reviewId, CancellationToken cancellationToken)
    {
        var markdown = await ExportToMarkdownAsync(reviewId, cancellationToken);
        return GenerateBytesFromMarkdown(markdown);
    }

    public async Task DeleteAsync(Guid id, int userId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.LiteratureReviews.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(LiteratureReview), id);

        dbContext.LiteratureReviews.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted literature review {ReviewId}", id);
    }

    private static List<LiteratureReviewSection> ParseSections(string? contentJson)
    {
        if (string.IsNullOrEmpty(contentJson))
        {
            return [];
        }

        try
        {
            var node = JsonNode.Parse(contentJson);
            var sectionsArray = node?["sections"]?.AsArray();
            if (sectionsArray is null)
            {
                return [];
            }

            return sectionsArray.Select(s => new LiteratureReviewSection(
                s?["heading"]?.GetValue<string>() ?? string.Empty,
                s?["content"]?.GetValue<string>() ?? string.Empty,
                s?["citedPaperIds"]?.AsArray()?.Select(v => v?.GetValue<Guid>() ?? Guid.Empty).ToList() ?? [])).ToList();
        }
        catch
        {
            return [];
        }
    }

    private static string RenderToMarkdown(string title, string researchQuestion, JsonNode json)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"**Research Question:** {researchQuestion}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        var sections = json["sections"]?.AsArray();
        if (sections is not null)
        {
            foreach (var section in sections)
            {
                var heading = section?["heading"]?.GetValue<string>() ?? "Section";
                var content = section?["content"]?.GetValue<string>() ?? string.Empty;

                sb.AppendLine($"## {heading}");
                sb.AppendLine();
                sb.AppendLine(content);
                sb.AppendLine();

                var citedIds = section?["citedPaperIds"]?.AsArray();
                if (citedIds is not null && citedIds.Count > 0)
                {
                    sb.AppendLine("*Cited papers: " + string.Join(", ", citedIds.Select(v => $"[{v?.GetValue<Guid>()}]")) + "*");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    private static byte[] GenerateBytesFromMarkdown(string markdown)
    {
        return System.Text.Encoding.UTF8.GetBytes(markdown);
    }
}