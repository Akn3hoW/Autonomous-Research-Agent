using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Summaries;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Summaries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class SummariesController(ISummaryService summaryService) : ControllerBase
{
    [HttpGet("papers/{id:guid}/summaries")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyCollection<SummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SummaryDto>>> GetSummariesForPaper(Guid id, CancellationToken cancellationToken)
    {
        var result = await summaryService.ListForPaperAsync(id, cancellationToken);
        return Ok(result.Select(s => s.ToDto()).ToList());
    }

    [HttpPost("papers/{id:guid}/summaries")]
    [Authorize(Policy = PolicyNames.EditAccess)]
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
}

