using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Analysis;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Analysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route("api/v1/analysis")]
public sealed class AnalysisController(IAnalysisService analysisService) : ControllerBase
{
    [HttpPost("compare-papers")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResultDto>> ComparePapers([FromBody] ComparePapersRequest request, CancellationToken cancellationToken)
    {
        var result = await analysisService.ComparePapersAsync(request.ToApplicationModel(User.GetActorName()), cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPost("compare-fields")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResultDto>> CompareFields([FromBody] CompareFieldsRequest request, CancellationToken cancellationToken)
    {
        var result = await analysisService.CompareFieldsAsync(request.ToApplicationModel(User.GetActorName()), cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPost("generate-insights")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(AnalysisJobStatusDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<AnalysisJobStatusDto>> GenerateInsights([FromBody] GenerateInsightsRequest request, CancellationToken cancellationToken)
    {
        var result = await analysisService.GenerateInsightsAsync(request.ToApplicationModel(User.GetActorName()), cancellationToken);
        return AcceptedAtAction(nameof(GetAnalysisByJobId), new { jobId = result.JobId }, result.ToDto());
    }

    [HttpGet("{jobId:guid}")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(AnalysisJobStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisJobStatusDto>> GetAnalysisByJobId(Guid jobId, CancellationToken cancellationToken)
    {
        var result = await analysisService.GetByJobIdAsync(jobId, cancellationToken);
        return Ok(result.ToDto());
    }
}
