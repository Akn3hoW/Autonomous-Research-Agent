using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Watchlist;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Watchlist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/saved-searches")]
public sealed class SavedSearchesController(ISavedSearchService savedSearchService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<SavedSearchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SavedSearchDto>>> GetSavedSearches(
        [FromQuery] SavedSearchQueryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await savedSearchService.ListAsync(request.ToApplicationModel(userId), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedSearchDto>> GetSavedSearch(Guid id, CancellationToken cancellationToken)
    {
        var savedSearch = await savedSearchService.GetByIdAsync(id, cancellationToken);
        return Ok(savedSearch.ToDto());
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SavedSearchDto>> CreateSavedSearch(
        [FromBody] CreateSavedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var created = await savedSearchService.CreateAsync(request.ToApplicationModel(userId), cancellationToken);
        return CreatedAtAction(nameof(GetSavedSearch), new { id = created.Id }, created.ToDto());
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(SavedSearchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SavedSearchDto>> UpdateSavedSearch(
        Guid id,
        [FromBody] UpdateSavedSearchRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await savedSearchService.UpdateAsync(id, request.ToApplicationModel(), cancellationToken);
        return Ok(updated.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSavedSearch(Guid id, CancellationToken cancellationToken)
    {
        await savedSearchService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/run")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(RunSavedSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunSavedSearchResponse>> RunSavedSearch(Guid id, CancellationToken cancellationToken)
    {
        var result = await savedSearchService.RunAsync(id, cancellationToken);
        return Ok(result.ToDto());
    }

    private int? GetUserId() => User.GetUserId();
}
