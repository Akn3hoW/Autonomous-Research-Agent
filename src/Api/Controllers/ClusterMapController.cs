using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Clustering;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Clustering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/papers")]
public sealed class ClusterMapController(IClusteringService clusteringService) : ControllerBase
{
    [HttpGet("cluster-map")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(ClusterMapResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClusterMapResponseDto>> GetClusterMap(CancellationToken cancellationToken)
    {
        var result = await clusteringService.GetClusterMapAsync(cancellationToken);

        var response = new ClusterMapResponseDto(
            result.Papers
                .Where(p => p.ClusterX != null && p.ClusterY != null)
                .Select(p => new PaperClusterDto(
                    p.Id,
                    p.Title,
                    p.Abstract,
                    p.Authors.ToList(),
                    p.Year,
                    p.ClusterX!.Value,
                    p.ClusterY!.Value)).ToList(),
            result.TotalCount);

        return Ok(response);
    }

    [HttpPost("cluster-map/jobs")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<Guid>> CreateClusteringJob(CancellationToken cancellationToken)
    {
        var jobId = await clusteringService.CreateJobAsync(User.GetActorName(), cancellationToken);
        return Accepted(jobId);
    }
}
