using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.LiteratureReviews;
using AutonomousResearchAgent.Application.LiteratureReviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/literature-reviews")]
public sealed class LiteratureReviewsController(ILiteratureReviewService literatureReviewService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyList<LiteratureReviewListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LiteratureReviewListItemDto>>> GetReviews(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var reviews = await literatureReviewService.ListAsync(userId, cancellationToken);
        return Ok(reviews.Select(r => new LiteratureReviewListItemDto(
            r.Id,
            r.Title,
            r.ResearchQuestion,
            r.Status.ToString(),
            r.PaperIds.Count,
            r.CreatedAt,
            r.CompletedAt)).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(LiteratureReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LiteratureReviewDto>> GetReview(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var review = await literatureReviewService.GetByIdAsync(id, userId, cancellationToken);
        if (review is null)
        {
            return NotFound();
        }

        return Ok(new LiteratureReviewDto(
            review.Id,
            review.Title,
            review.ResearchQuestion,
            review.Sections.Select(s => new Api.Contracts.LiteratureReviews.LiteratureReviewSection(s.Heading, s.Content, s.CitedPaperIds)).ToList(),
            review.Status.ToString(),
            review.PaperIds,
            review.CreatedAt,
            review.CompletedAt));
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(LiteratureReviewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LiteratureReviewDto>> CreateReview([FromBody] CreateLiteratureReviewRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new CreateLiteratureReviewCommand(request.Title, request.ResearchQuestion, request.PaperIds);
        var created = await literatureReviewService.CreateAsync(command, userId, cancellationToken);
        return CreatedAtAction(nameof(GetReview), new { id = created.Id }, new LiteratureReviewDto(
            created.Id,
            created.Title,
            created.ResearchQuestion,
            created.Sections.Select(s => new Api.Contracts.LiteratureReviews.LiteratureReviewSection(s.Heading, s.Content, s.CitedPaperIds)).ToList(),
            created.Status.ToString(),
            created.PaperIds,
            created.CreatedAt,
            created.CompletedAt));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteReview(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await literatureReviewService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/export/markdown")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FileResult>> ExportMarkdown(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var review = await literatureReviewService.GetByIdAsync(id, userId, cancellationToken);
        if (review is null)
            return NotFound();
        var markdown = await literatureReviewService.ExportToMarkdownAsync(id, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(markdown), "text/markdown", $"literature_review_{id}.md");
    }

    [HttpGet("{id:guid}/export/pdf")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FileResult>> ExportPdf(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var review = await literatureReviewService.GetByIdAsync(id, userId, cancellationToken);
        if (review is null)
            return NotFound();
        var pdfBytes = await literatureReviewService.ExportToPdfAsync(id, cancellationToken);
        return File(pdfBytes, "application/pdf", $"literature_review_{id}.pdf");
    }

    private int? GetUserId() => User.GetUserId();
}