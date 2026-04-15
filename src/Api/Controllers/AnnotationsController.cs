using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Annotations;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}")]
public sealed class AnnotationsController(IAnnotationService annotationService) : ControllerBase
{
    [HttpGet("papers/{paperId:guid}/annotations")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AnnotationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AnnotationResponse>>> GetAnnotationsForPaper(
        Guid paperId,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized();
        var annotations = await annotationService.ListForPaperAsync(paperId, userId.Value, cancellationToken);
        return Ok(annotations.Select(a => new AnnotationResponse(
            a.Id,
            a.PaperId,
            a.UserId,
            a.UserName,
            a.HighlightedText,
            a.Note,
            a.PageNumber,
            a.OffsetStart,
            a.OffsetEnd,
            a.CreatedAt,
            a.UpdatedAt
        )).ToList());
    }

    [HttpPost("papers/{paperId:guid}/annotations")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(AnnotationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnotationResponse>> CreateAnnotation(
        Guid paperId,
        [FromBody] CreateAnnotationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
            throw new AuthenticationException("User ID not found in token");

        var command = new CreateAnnotationCommand(
            paperId,
            userId.Value,
            request.ChunkId,
            request.Page,
            request.OffsetStart,
            request.OffsetEnd,
            request.HighlightedText,
            request.Note
        );

        var created = await annotationService.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetAnnotation), new { id = created.Id }, new AnnotationResponse(
            created.Id,
            created.PaperId,
            created.UserId,
            created.UserName,
            created.HighlightedText,
            created.Note,
            created.PageNumber,
            created.OffsetStart,
            created.OffsetEnd,
            created.CreatedAt,
            created.UpdatedAt
        ));
    }

    [HttpGet("annotations/{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(AnnotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnotationResponse>> GetAnnotation(Guid id, CancellationToken cancellationToken)
    {
        var annotation = await annotationService.GetByIdAsync(id, cancellationToken);
        return Ok(new AnnotationResponse(
            annotation.Id,
            annotation.PaperId,
            annotation.UserId,
            annotation.UserName,
            annotation.HighlightedText,
            annotation.Note,
            annotation.PageNumber,
            annotation.OffsetStart,
            annotation.OffsetEnd,
            annotation.CreatedAt,
            annotation.UpdatedAt
        ));
    }

    [HttpPut("annotations/{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(AnnotationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AnnotationResponse>> UpdateAnnotation(
        Guid id,
        [FromBody] UpdateAnnotationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAnnotationCommand(request.Note);
        var updated = await annotationService.UpdateAsync(id, command, cancellationToken);
        return Ok(new AnnotationResponse(
            updated.Id,
            updated.PaperId,
            updated.UserId,
            updated.UserName,
            updated.HighlightedText,
            updated.Note,
            updated.PageNumber,
            updated.OffsetStart,
            updated.OffsetEnd,
            updated.CreatedAt,
            updated.UpdatedAt
        ));
    }

    [HttpDelete("annotations/{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnnotation(Guid id, CancellationToken cancellationToken)
    {
        await annotationService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}