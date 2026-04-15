using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Papers;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Duplicates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/papers/duplicates")]
public sealed class DuplicateDetectionController(IDuplicateDetectionService duplicateDetectionService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(DuplicatesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DuplicatesResponse>> GetPotentialDuplicates(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await duplicateDetectionService.GetPotentialDuplicatesAsync(page, pageSize, cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpGet("pending")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(DuplicatesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DuplicatesResponse>> GetPendingDuplicates(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await duplicateDetectionService.GetPendingDuplicatesAsync(page, pageSize, cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPut("{id:guid}/resolve")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveDuplicate(
        Guid id,
        [FromBody] ResolveDuplicateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await duplicateDetectionService.ResolveDuplicateAsync(
            id,
            request.IsDuplicate,
            request.MergedIntoPaperId,
            request.Notes,
            userId,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/merge")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MergeDuplicate(
        Guid id,
        [FromBody] MergeDuplicateRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await duplicateDetectionService.MergeDuplicatePapersAsync(
            request.KeepPaperId,
            request.MergeIntoPaperId,
            request.Notes,
            userId,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("detect")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<Guid>> StartDuplicateDetection(
        [FromQuery] double threshold = 0.95,
        CancellationToken cancellationToken = default)
    {
        var jobId = await duplicateDetectionService.StartDuplicateDetectionJobAsync(threshold, cancellationToken);
        return Accepted(jobId);
    }
}
