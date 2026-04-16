using System.Text.Json.Nodes;
using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Jobs;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/jobs")]
public sealed class JobsController(IJobService jobService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<JobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<JobDto>>> GetJobs([FromQuery] JobQueryRequest request, CancellationToken cancellationToken)
    {
        var result = await jobService.ListAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JobDto>> GetJob(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobService.GetByIdAsync(id, cancellationToken);
        return Ok(job.ToDto());
    }

    [HttpPost("import-papers")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.JobCreation)]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<JobDto>> CreateImportJob([FromBody] CreateImportJobRequest request, CancellationToken cancellationToken)
    {
        var queriesArray = new JsonArray();
        foreach (var query in request.Queries)
        {
            queriesArray.Add(JsonValue.Create(query));
        }

        var payload = new JsonObject
        {
            ["queries"] = queriesArray,
            ["limit"] = request.Limit,
            ["storeImportedPapers"] = request.StoreImportedPapers
        };

        var job = await jobService.CreateAsync(
            new CreateJobCommand(JobType.ImportPapers, payload, null, User.GetActorName()),
            cancellationToken);

        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, job.ToDto());
    }

    [HttpPost("summarize-paper")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.JobCreation)]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<JobDto>> CreateSummarizeJob([FromBody] CreateSummarizeJobRequest request, CancellationToken cancellationToken)
    {
        var payload = new JsonObject
        {
            ["paperId"] = request.PaperId,
            ["modelName"] = request.ModelName,
            ["promptVersion"] = request.PromptVersion
        };

        var job = await jobService.CreateAsync(
            new CreateJobCommand(JobType.SummarizePaper, payload, request.PaperId, User.GetActorName()),
            cancellationToken);

        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, job.ToDto());
    }

    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<JobDto>> RetryJob(Guid id, [FromBody] RetryJobRequest request, CancellationToken cancellationToken)
    {
        var result = await jobService.RetryAsync(id, new RetryJobCommand(User.GetActorName(), request.Reason), cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(JobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobDto>> CancelJob(Guid id, CancellationToken cancellationToken)
    {
        var result = await jobService.CancelAsync(id, cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteJob(Guid id, CancellationToken cancellationToken)
    {
        await jobService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("parent/{parentId:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(List<JobDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<JobDto>>> GetJobsByParentId(Guid parentId, CancellationToken cancellationToken)
    {
        var jobs = await jobService.GetJobsByParentIdAsync(parentId, cancellationToken);
        return Ok(jobs.Select(j => j.ToDto()).ToList());
    }
}
