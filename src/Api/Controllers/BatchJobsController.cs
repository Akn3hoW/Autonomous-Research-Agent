using System.Text.Json.Nodes;
using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.BatchJobs;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.BatchJobs;
using AutonomousResearchAgent.Application.Collections;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class BatchJobsController(
    IBatchJobService batchJobService,
    IJobService jobService,
    IPaperService paperService,
    ICollectionService collectionService) : ControllerBase
{
    [HttpPost("papers/batch")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(typeof(BatchJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchJobDto>> CreateBatchJob([FromBody] BatchOperationRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId() ?? 0;

        if (request.PaperIds.Count == 0)
        {
            return BadRequest(new ProblemDetails { Title = "At least one paperId is required" });
        }

        var validActions = new[] { "summarize", "add-to-collection", "tag", "delete" };
        if (!validActions.Contains(request.Action.ToLowerInvariant()))
        {
            return BadRequest(new ProblemDetails { Title = $"Action must be one of: {string.Join(", ", validActions)}" });
        }

        var batchJob = await batchJobService.CreateAsync(
            new CreateBatchJobCommand(request.Action.ToLowerInvariant(), userId, request.PaperIds.Count),
            cancellationToken);

        var normalizedAction = request.Action.ToLowerInvariant();

        foreach (var paperId in request.PaperIds)
        {
            JsonObject payload;
            JobType jobType;
            Guid? targetEntityId = paperId;

            switch (normalizedAction)
            {
                case "summarize":
                    jobType = JobType.SummarizePaper;
                    var modelName = request.Params.GetValueOrDefault("modelName")?.ToString() ?? "openrouter/hunter-alpha";
                    var promptVersion = request.Params.GetValueOrDefault("promptVersion")?.ToString() ?? string.Empty;
                    payload = new JsonObject
                    {
                        ["paperId"] = paperId.ToString(),
                        ["modelName"] = modelName,
                        ["promptVersion"] = promptVersion,
                        ["batchJobId"] = batchJob.Id.ToString()
                    };
                    break;

                case "add-to-collection":
                    jobType = JobType.Analysis;
                    var collectionIdStr = request.Params.GetValueOrDefault("collectionId")?.ToString();
                    if (!Guid.TryParse(collectionIdStr, out var collectionId))
                    {
                        return BadRequest(new ProblemDetails { Title = "collectionId is required for add-to-collection action" });
                    }
                    payload = new JsonObject
                    {
                        ["collectionId"] = collectionId.ToString(),
                        ["paperId"] = paperId.ToString(),
                        ["batchJobId"] = batchJob.Id.ToString()
                    };
                    targetEntityId = collectionId;
                    break;

                case "tag":
                    jobType = JobType.Analysis;
                    var tags = request.Params.GetValueOrDefault("tags")?.ToString() ?? "[]";
                    payload = new JsonObject
                    {
                        ["paperId"] = paperId.ToString(),
                        ["tags"] = tags,
                        ["batchJobId"] = batchJob.Id.ToString()
                    };
                    break;

                case "delete":
                    jobType = JobType.Analysis;
                    payload = new JsonObject
                    {
                        ["paperId"] = paperId.ToString(),
                        ["action"] = "delete",
                        ["batchJobId"] = batchJob.Id.ToString()
                    };
                    break;

                default:
                    continue;
            }

            await jobService.CreateAsync(
                new CreateJobCommand(jobType, payload, targetEntityId, User.GetActorName(), batchJob.Id),
                cancellationToken);
        }

        return AcceptedAtAction(nameof(GetBatchJob), new { id = batchJob.Id }, new BatchJobDto(
            batchJob.Id,
            batchJob.Action,
            batchJob.Status,
            batchJob.Total,
            batchJob.Completed,
            batchJob.CreatedAt,
            batchJob.UpdatedAt));
    }

    [HttpGet("batch-jobs/{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(BatchJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchJobDto>> GetBatchJob(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId() ?? 0;
        var batchJob = await batchJobService.GetByIdAsync(id, userId, cancellationToken);
        return Ok(new BatchJobDto(
            batchJob.Id,
            batchJob.Action,
            batchJob.Status,
            batchJob.Total,
            batchJob.Completed,
            batchJob.CreatedAt,
            batchJob.UpdatedAt));
    }
}