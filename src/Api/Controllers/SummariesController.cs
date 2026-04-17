using System.Text;
using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Summaries;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Api.Middleware;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Summaries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}")]
public sealed class SummariesController(
    ISummaryService summaryService,
    ISummaryDiffService summaryDiffService,
    ISummarizationService summarizationService,
    IPromptVersionService promptVersionService) : ControllerBase
{
    [HttpGet("papers/{id:guid}/summaries")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyCollection<SummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SummaryDto>>> GetSummariesForPaper(Guid id, CancellationToken cancellationToken)
    {
        var result = await summaryService.ListForPaperAsync(id, cancellationToken);
        return Ok(result.Select(s => s.ToDto()).ToList());
    }

    [HttpGet("papers/{paperId:guid}/summaries/diff")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(SummaryDiffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SummaryDiffDto>> GetSummaryDiff(Guid paperId, Guid v1, Guid v2, CancellationToken cancellationToken)
    {
        var result = await summaryDiffService.ComputeDiffAsync(paperId, v1, v2, cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPost("papers/{id:guid}/summaries")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(typeof(SummaryDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SummaryDto>> CreateSummary(Guid id, [FromBody] CreateSummaryRequest request, CancellationToken cancellationToken)
    {
        var created = await summaryService.CreateAsync(request.ToApplicationModel(id), cancellationToken);
        return CreatedAtAction(nameof(GetSummary), new { summaryId = created.Id }, created.ToDto());
    }

    [HttpGet("summaries/{summaryId:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(SummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SummaryDto>> GetSummary(Guid summaryId, CancellationToken cancellationToken)
    {
        var result = await summaryService.GetByIdAsync(summaryId, cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPatch("summaries/{summaryId:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(SummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SummaryDto>> UpdateSummary(Guid summaryId, [FromBody] UpdateSummaryRequest request, CancellationToken cancellationToken)
    {
        var updated = await summaryService.UpdateAsync(summaryId, request.ToApplicationModel(), cancellationToken);
        return Ok(updated.ToDto());
    }

    [HttpPost("summaries/{summaryId:guid}/approve")]
    [Authorize(Policy = PolicyNames.ReviewAccess)]
    [ProducesResponseType(typeof(SummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SummaryDto>> ApproveSummary(Guid summaryId, [FromBody] ReviewSummaryRequest request, CancellationToken cancellationToken)
    {
        var reviewed = await summaryService.ReviewAsync(summaryId, request.ToApprovedReviewCommand(User.GetActorName()), cancellationToken);
        return Ok(reviewed.ToDto());
    }

    [HttpPost("summaries/{summaryId:guid}/reject")]
    [Authorize(Policy = PolicyNames.ReviewAccess)]
    [ProducesResponseType(typeof(SummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SummaryDto>> RejectSummary(Guid summaryId, [FromBody] ReviewSummaryRequest request, CancellationToken cancellationToken)
    {
        var reviewed = await summaryService.ReviewAsync(summaryId, request.ToRejectedReviewCommand(User.GetActorName()), cancellationToken);
        return Ok(reviewed.ToDto());
    }

    [HttpDelete("summaries/{summaryId:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSummary(Guid summaryId, CancellationToken cancellationToken)
    {
        await summaryService.DeleteAsync(summaryId, cancellationToken);
        return NoContent();
    }

    [HttpPost("papers/{paperId:guid}/summaries/ab-test")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(typeof(AbTestSessionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AbTestSessionDto>> CreateAbTest(Guid paperId, [FromBody] Contracts.Summaries.CreateAbTestRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var appRequest = request.ToApplicationModel();
        var created = await summarizationService.CreateAbTestSessionAsync(appRequest with { PaperId = paperId }, userId, cancellationToken);
        return CreatedAtAction(nameof(GetAbTestSession), new { sessionId = created.Id }, created.ToDto());
    }

    [HttpGet("summaries/ab-test/{sessionId:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(AbTestSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AbTestSessionDto>> GetAbTestSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await summarizationService.GetAbTestSessionAsync(sessionId, cancellationToken);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result.ToDto());
    }

    [HttpGet("papers/{paperId:guid}/summaries/ab-test")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyCollection<AbTestSessionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<AbTestSessionDto>>> GetAbTestsForPaper(Guid paperId, CancellationToken cancellationToken)
    {
        var sessions = await summarizationService.GetAbTestSessionsForPaperAsync(paperId, cancellationToken);
        return Ok(sessions.Select(s => s.ToDto()).ToList());
    }

    [HttpPost("summaries/ab-test/{sessionId:guid}/select/{summaryId:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(AbTestSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AbTestSessionDto>> SelectAbTestResult(Guid sessionId, Guid summaryId, CancellationToken cancellationToken)
    {
        var result = await summarizationService.SelectAbTestResultAsync(sessionId, summaryId, cancellationToken);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result.ToDto());
    }

    [HttpGet("papers/{paperId:guid}/summaries/{summaryId:guid}/stream")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task StreamSummary(Guid paperId, Guid summaryId, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.StatusCode = 200;

        try
        {
            var summary = await summaryService.GetByIdAsync(summaryId, cancellationToken);
            var text = summary.Summary?.ToJsonString() ?? "Summary not available.";

            var words = text.Split(' ');
            foreach (var word in words)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var bytes = Encoding.UTF8.GetBytes($"data: {word} ");
                await Response.Body.WriteAsync(bytes, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(20, cancellationToken);
            }

            var endBytes = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
            await Response.Body.WriteAsync(endBytes, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch
        {
            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
            }
        }
    }

    [HttpPost("summaries/prompt-versions")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(PromptVersionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PromptVersionDto>> CreatePromptVersion([FromBody] CreatePromptVersionRequest request, CancellationToken cancellationToken)
    {
        var command = request.ToApplicationModel();
        var created = await promptVersionService.CreateAsync(command, cancellationToken);
        return CreatedAtAction(nameof(GetPromptVersion), new { id = created.Id }, created.ToDto());
    }

    [HttpGet("summaries/prompt-versions")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyCollection<PromptVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<PromptVersionDto>>> GetPromptVersions(CancellationToken cancellationToken)
    {
        var versions = await promptVersionService.ListAsync(cancellationToken);
        return Ok(versions.Select(v => v.ToDto()).ToList());
    }

    [HttpGet("summaries/prompt-versions/{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PromptVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromptVersionDto>> GetPromptVersion(Guid id, CancellationToken cancellationToken)
    {
        var version = await promptVersionService.GetByIdAsync(id, cancellationToken);
        if (version == null)
        {
            return NotFound();
        }
        return Ok(version.ToDto());
    }

    [HttpGet("summaries/prompt-versions/active")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PromptVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromptVersionDto>> GetActivePromptVersion(CancellationToken cancellationToken)
    {
        var version = await promptVersionService.GetActiveVersionAsync(cancellationToken);
        if (version == null)
        {
            return NotFound();
        }
        return Ok(version.ToDto());
    }

    [HttpPost("summaries/prompt-versions/{id:guid}/activate")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(PromptVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromptVersionDto>> ActivatePromptVersion(Guid id, CancellationToken cancellationToken)
    {
        var version = await promptVersionService.SetActiveAsync(id, cancellationToken);
        return Ok(version.ToDto());
    }

    [HttpPost("summaries/prompt-versions/{id:guid}/deactivate")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(PromptVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromptVersionDto>> DeactivatePromptVersion(Guid id, CancellationToken cancellationToken)
    {
        var version = await promptVersionService.DeactivateAsync(id, cancellationToken);
        return Ok(version.ToDto());
    }

    private Guid GetUserId()
    {
        var userId = User.GetUserGuid();
        return userId ?? throw new AuthenticationException("User ID not found in token.");
    }
}