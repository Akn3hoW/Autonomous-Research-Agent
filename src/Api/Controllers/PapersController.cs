using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Papers;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Api.Middleware;
using AutonomousResearchAgent.Application.Citations;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Papers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/papers")]
public sealed class PapersController(
    IPaperService paperService,
    ICitationGraphService citationGraphService) : ControllerBase
{
    /// <summary>
    /// Lists papers using pagination, filtering, and sorting.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<PaperListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<PaperListItemDto>>> GetPapers([FromQuery] PaperQueryRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await paperService.ListAsync(request.ToApplicationModel(), userId, cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PaperDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaperDetailDto>> GetPaper(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var paper = await paperService.GetByIdAsync(id, null, cancellationToken);
        return Ok(paper.ToDto());
    }

    /// <summary>
    /// Creates a paper record manually.
    /// </summary>
    [HttpPost]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(PaperDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaperDetailDto>> CreatePaper([FromBody] CreatePaperRequest request, CancellationToken cancellationToken)
    {
        var created = await paperService.CreateAsync(request.ToApplicationModel(), cancellationToken);
        return CreatedAtAction(nameof(GetPaper), new { id = created.Id }, created.ToDto());
    }

    /// <summary>
    /// Updates editable paper metadata.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(PaperDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaperDetailDto>> UpdatePaper(Guid id, [FromBody] UpdatePaperRequest request, CancellationToken cancellationToken)
    {
        var updated = await paperService.UpdateAsync(id, request.ToApplicationModel(), cancellationToken);
        return Ok(updated.ToDto());
    }

    /// <summary>
    /// Imports papers synchronously via the Semantic Scholar integration.
    /// </summary>
    [HttpPost("import")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(typeof(ImportPapersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportPapersResponse>> ImportPapers([FromBody] ImportPapersRequest request, CancellationToken cancellationToken)
    {
        var result = await paperService.ImportAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToDto());
    }

    /// <summary>
    /// Deletes a paper by its internal identifier.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePaper(Guid id, CancellationToken cancellationToken)
    {
        await paperService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Gets the citation graph for a paper.
    /// </summary>
    [HttpGet("{id:guid}/graph")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(CitationGraphResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CitationGraphResponse>> GetCitationGraph(Guid id, [FromQuery][Range(1, 10)] int depth = 2, CancellationToken cancellationToken = default)
    {
        var graph = await citationGraphService.GetCitationGraphAsync(id, depth, cancellationToken);
        return Ok(graph.ToDto());
    }

    /// <summary>
    /// Ingests citations for a paper from Semantic Scholar.
    /// </summary>
    [HttpPost("{id:guid}/ingest-citations")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> IngestCitations(Guid id, CancellationToken cancellationToken)
    {
        await citationGraphService.IngestCitationsAsync(id, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var userId = User.GetUserGuid();
        return userId ?? throw new AuthenticationException("User ID not found in token.");
    }
}

