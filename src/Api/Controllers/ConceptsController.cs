using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Concepts;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Concepts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/concepts")]
public sealed class ConceptsController(IConceptService conceptService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<ConceptDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ConceptDto>>> GetConcepts(
        [FromQuery] ConceptQueryRequest request,
        CancellationToken cancellationToken)
    {
        var query = new ConceptQuery(
            request.PageNumber,
            request.PageSize,
            ParseNullableConceptType(request.ConceptType),
            request.PaperId,
            request.Search);

        var result = await conceptService.ListAsync(query, cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpGet("statistics")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(ConceptStatisticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConceptStatisticsDto>> GetStatistics(CancellationToken cancellationToken)
    {
        var stats = await conceptService.GetStatisticsAsync(cancellationToken);
        return Ok(stats.ToDto());
    }

    [HttpPost("jobs")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<Guid>> CreateExtractionJob(CancellationToken cancellationToken)
    {
        var jobId = await conceptService.CreateJobAsync(User.GetActorName(), cancellationToken);
        return Accepted(jobId);
    }

    private static Domain.Enums.ConceptType? ParseNullableConceptType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Enum.TryParse<Domain.Enums.ConceptType>(value, true, out var parsed) ? parsed : null;
    }
}
