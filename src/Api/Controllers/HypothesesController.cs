using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Api.Middleware;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Hypotheses;
using AutonomousResearchAgent.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/hypotheses")]
public sealed class HypothesesController(IHypothesisService hypothesisService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyList<HypothesisResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HypothesisResponse>>> GetHypotheses(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var results = await hypothesisService.GetAllByUserAsync(userId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(HypothesisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HypothesisResponse>> GetHypothesis(Guid id, CancellationToken cancellationToken)
    {
        var result = await hypothesisService.GetByIdAsync(id, cancellationToken);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(HypothesisResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HypothesisResponse>> CreateHypothesis([FromBody] CreateHypothesisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = GetUserId();
        var command = new CreateHypothesisCommand(
            request.Title,
            request.Description,
            userId,
            request.InitialPapers?.Select(p => new Application.Hypotheses.HypothesisPaperInput(p.PaperId, ParseEvidenceType(p.EvidenceType), p.EvidenceText)).ToList());
        var created = await hypothesisService.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetHypothesis), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(HypothesisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HypothesisResponse>> UpdateHypothesis(Guid id, [FromBody] UpdateHypothesisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = GetUserId();
        var existing = await hypothesisService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound();
        if (existing.UserId != userId)
            return Forbid();
        var command = new UpdateHypothesisCommand(request.Title, request.Description);
        var updated = await hypothesisService.UpdateAsync(id, command, cancellationToken);
        return Ok(updated);
    }

    [HttpPut("{id:guid}/status")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(HypothesisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HypothesisResponse>> UpdateHypothesisStatus(Guid id, [FromBody] UpdateHypothesisStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var status = ParseHypothesisStatus(request.Status);
        var command = new UpdateHypothesisStatusCommand(status, request.EvidenceText);
        var updated = await hypothesisService.UpdateStatusAsync(id, command, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteHypothesis(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var existing = await hypothesisService.GetByIdAsync(id, cancellationToken);
        if (existing == null)
            return NotFound();
        if (existing.UserId != userId)
            return Forbid();
        await hypothesisService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/papers")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(HypothesisPaperResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HypothesisPaperResponse>> AddPaper(Guid id, [FromBody] AddHypothesisPaperRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new AddHypothesisPaperCommand(request.PaperId, ParseEvidenceType(request.EvidenceType), request.EvidenceText);
        var result = await hypothesisService.AddPaperAsync(id, command, cancellationToken);
        return CreatedAtAction(nameof(GetHypothesis), new { id }, result);
    }

    [HttpDelete("{id:guid}/papers/{paperId:guid}")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemovePaper(Guid id, Guid paperId, CancellationToken cancellationToken)
    {
        await hypothesisService.DeletePaperAsync(id, paperId, cancellationToken);
        return NoContent();
    }

    private static HypothesisStatus ParseHypothesisStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "supported" => HypothesisStatus.Supported,
            "refuted" => HypothesisStatus.Refuted,
            "open" => HypothesisStatus.Open,
            _ => HypothesisStatus.Proposed
        };
    }

    private static EvidenceType ParseEvidenceType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "refuting" => EvidenceType.Refuting,
            _ => EvidenceType.Supporting
        };
    }

    private Guid GetUserId()
    {
        var userId = User.GetUserGuid();
        return userId ?? throw new AuthenticationException("User ID not found in token.");
    }
}

public sealed record CreateHypothesisRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<HypothesisPaperInput>? InitialPapers { get; init; }
}

public sealed record UpdateHypothesisRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
}

public sealed record UpdateHypothesisStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? EvidenceText { get; init; }
}

public sealed record AddHypothesisPaperRequest
{
    public Guid PaperId { get; init; }
    public string EvidenceType { get; init; } = "Supporting";
    public string? EvidenceText { get; init; }
}

public sealed record HypothesisPaperInput(Guid PaperId, string EvidenceType, string? EvidenceText);