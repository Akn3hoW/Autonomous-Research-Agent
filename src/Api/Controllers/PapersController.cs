using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Papers;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Papers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route("api/v1/papers")]
public sealed class PapersController(IPaperService paperService) : ControllerBase
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
        var result = await paperService.ListAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    /// <summary>
    /// Gets a paper by its internal identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PaperDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaperDetailDto>> GetPaper(Guid id, CancellationToken cancellationToken)
    {
        var paper = await paperService.GetByIdAsync(id, cancellationToken);
        return Ok(paper.ToDto());
    }

    /// <summary>
    /// Creates a paper record manually.
    /// </summary>
    [HttpPost]
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
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(ImportPapersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportPapersResponse>> ImportPapers([FromBody] ImportPapersRequest request, CancellationToken cancellationToken)
    {
        var result = await paperService.ImportAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToDto());
    }
}

