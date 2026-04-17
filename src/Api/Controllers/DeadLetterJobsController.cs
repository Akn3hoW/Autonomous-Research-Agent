using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/dead-letter-jobs")]
public sealed class DeadLetterJobsController : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<DeadLetterJobDto>), StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<DeadLetterJobDto>>> GetDeadLetterJobs(
        [FromQuery] DeadLetterJobQueryRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Task.FromResult<ActionResult<PagedResponse<DeadLetterJobDto>>>(Ok(new PagedResponse<DeadLetterJobDto>([], request.PageNumber, request.PageSize, 0)));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(DeadLetterJobDto), StatusCodes.Status200OK)]
    public Task<ActionResult<DeadLetterJobDto>> GetDeadLetterJob(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<DeadLetterJobDto>>(NotFound());
    }

    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    public Task<ActionResult<JobDto>> RetryDeadLetterJob(Guid id, [FromBody] RetryDeadLetterJobRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<JobDto>>(NotFound());
    }

    [HttpPost("{id:guid}/process")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(DeadLetterJobDto), StatusCodes.Status200OK)]
    public Task<ActionResult<DeadLetterJobDto>> ProcessDeadLetterJob(Guid id, [FromBody] ProcessDeadLetterJobRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<DeadLetterJobDto>>(NotFound());
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> DeleteDeadLetterJob(Guid id, CancellationToken cancellationToken)
    {
        return Task.FromResult<IActionResult>(NoContent());
    }

    [HttpGet("stats")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(DeadLetterJobStatsDto), StatusCodes.Status200OK)]
    public Task<ActionResult<DeadLetterJobStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        return Task.FromResult<ActionResult<DeadLetterJobStatsDto>>(Ok(new DeadLetterJobStatsDto(0, 0, 0, 0)));
    }
}

public sealed record DeadLetterJobStatsDto(
    int TotalCount,
    int PendingCount,
    int ProcessedCount,
    int ByExceptionTypeCount);
