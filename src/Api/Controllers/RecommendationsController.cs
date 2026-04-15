using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Recommendations;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Recommendations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/me")]
[Authorize(Policy = PolicyNames.ReadAccess)]
public sealed class RecommendationsController(IRecommendationService recommendationService) : ControllerBase
{
    [HttpGet("recommendations")]
    [ProducesResponseType(typeof(PagedResponse<PaperRecommendationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<PaperRecommendationResponse>>> GetRecommendations(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var query = new RecommendationQuery(userId.Value, pageNumber, pageSize);
        var result = await recommendationService.GetRecommendationsAsync(query, cancellationToken);

        return Ok(result.ToPagedResponse(MapToResponse));
    }

    private int? GetUserId() => User.GetUserId();

    private static PaperRecommendationResponse MapToResponse(PaperRecommendationModel model) =>
        new(
            model.PaperId,
            model.Title,
            model.Authors,
            model.Year,
            model.Venue,
            model.CitationCount,
            model.Status,
            model.SimilarityScore,
            model.CreatedAt);
}
