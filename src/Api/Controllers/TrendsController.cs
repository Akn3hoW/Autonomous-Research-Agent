using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Trends;
using AutonomousResearchAgent.Application.Trends;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/trends")]
public sealed class TrendsController(ITrendAnalysisService trendAnalysisService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(TrendsResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TrendsResponseDto>> GetTrends(
        [FromQuery] string? field,
        [FromQuery] int? startYear,
        [FromQuery] int? endYear,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var end = endYear ?? now.Year;
        var start = startYear ?? (end - 9);

        var request = new Application.Trends.TrendsRequest(field, start, end);
        var result = await trendAnalysisService.GetTrendsAsync(request, cancellationToken);

        var response = new TrendsResponseDto(
            result.Buckets.Select(b => new TrendBucketDto(
                b.Year,
                b.Topics.Select(t => new TrendTopicDto(
                    t.Topic,
                    t.Momentum,
                    t.PaperCount,
                    t.SamplePapers
                )).ToList()
            )).ToList(),
            result.EmergingThemes,
            result.DecliningThemes
        );

        return Ok(response);
    }

    [HttpPost("jobs")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<Guid>> StartTrendAnalysis(
        [FromQuery] string? field,
        [FromQuery] int? startYear,
        [FromQuery] int? endYear,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var end = endYear ?? now.Year;
        var start = startYear ?? (end - 9);

        var jobId = await trendAnalysisService.StartTrendAnalysisJobAsync(field, start, end, cancellationToken);
        return Accepted(jobId);
    }
}