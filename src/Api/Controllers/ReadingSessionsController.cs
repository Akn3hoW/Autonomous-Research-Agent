using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.ReadingSessions;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.ReadingSessions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/me")]
[Authorize(Policy = PolicyNames.ReadAccess)]
public sealed class ReadingSessionsController(IReadingSessionService readingSessionService) : ControllerBase
{
    [HttpGet("reading-list")]
    [ProducesResponseType(typeof(PagedResponse<ReadingSessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ReadingSessionResponse>>> GetReadingList(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var query = new ReadingSessionQuery(userId, status, pageNumber, pageSize);
        var result = await readingSessionService.ListAsync(query, cancellationToken);

        return Ok(result.ToPagedResponse(MapToResponse));
    }

    [HttpGet("reading-list/{id:guid}")]
    [ProducesResponseType(typeof(ReadingSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReadingSessionResponse>> GetReadingSession(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await readingSessionService.GetByIdAsync(id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }
        if (result.UserId != userId)
        {
            return Forbid();
        }
        return Ok(MapToResponse(result));
    }

    [HttpPost("reading-list")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(ReadingSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReadingSessionResponse>> CreateReadingSession(
        [FromBody] CreateReadingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new CreateReadingSessionCommand(userId, request.PaperId);
        var created = await readingSessionService.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetReadingSession), new { id = created.Id }, MapToResponse(created));
    }

    [HttpPut("reading-list/{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(ReadingSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ReadingSessionResponse>> UpdateReadingSession(
        Guid id,
        [FromBody] UpdateReadingSessionRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var existing = await readingSessionService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound();
        if (existing.UserId != userId)
            return Forbid();
        var command = new UpdateReadingSessionCommand(request.Status, request.Notes);
        var updated = await readingSessionService.UpdateAsync(id, command, cancellationToken);
        return Ok(MapToResponse(updated));
    }

    [HttpDelete("reading-list/{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteReadingSession(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var existing = await readingSessionService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound();
        if (existing.UserId != userId)
            return Forbid();
        await readingSessionService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private int? GetUserId() => User.GetUserId();

    private static ReadingSessionResponse MapToResponse(ReadingSessionModel model) =>
        new(
            model.Id,
            model.UserId,
            model.PaperId,
            model.PaperTitle,
            model.PaperAuthors,
            model.PaperYear,
            model.PaperVenue,
            model.PaperCitationCount,
            model.Status,
            model.Notes,
            model.StartedAt,
            model.FinishedAt,
            model.CreatedAt,
            model.UpdatedAt);
}
