using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Search;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/search")]
public sealed class SearchController(ISearchService searchService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<SearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SearchResultDto>>> Search([FromQuery] SearchRequest request, CancellationToken cancellationToken)
    {
        var result = await searchService.SearchAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpPost("semantic")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<SearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SearchResultDto>>> SemanticSearch([FromBody] SemanticSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await searchService.SemanticSearchAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpPost("hybrid")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<SearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SearchResultDto>>> HybridSearch([FromBody] HybridSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await searchService.HybridSearchAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpPost("chunks")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<ChunkSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ChunkSearchResultDto>>> SearchChunks([FromBody] ChunkSearchRequest request, CancellationToken cancellationToken)
    {
        var result = await searchService.SearchDocumentChunksAsync(request.ToApplicationModel(), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }
}

