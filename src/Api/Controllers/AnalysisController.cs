using System.Text;
using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Analysis;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Analysis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/analysis")]
public sealed class AnalysisController(IAnalysisService analysisService, ILogger<AnalysisController> logger) : ControllerBase
{
    [HttpPost("compare-papers")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResultDto>> ComparePapers([FromBody] ComparePapersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await analysisService.ComparePapersAsync(request.ToApplicationModel(User.GetActorName()), cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPost("compare-fields")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(typeof(AnalysisResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalysisResultDto>> CompareFields([FromBody] CompareFieldsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await analysisService.CompareFieldsAsync(request.ToApplicationModel(User.GetActorName()), cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpPost("generate-insights")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(typeof(AnalysisJobStatusDto), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<AnalysisJobStatusDto>> GenerateInsights([FromBody] GenerateInsightsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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

    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyList<AnalysisResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AnalysisResultDto>>> GetAnalyses(CancellationToken cancellationToken)
    {
        var results = await analysisService.GetAllAsync(cancellationToken);
        return Ok(results.Select(r => r.ToDto()).ToList());
    }

    [HttpDelete("{analysisResultId:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAnalysisResult(Guid analysisResultId, CancellationToken cancellationToken)
    {
        await analysisService.DeleteAsync(analysisResultId, cancellationToken);
        return NoContent();
    }

    [HttpPost("research-gap")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [EnableRateLimiting(RateLimiterPolicyNames.Expensive)]
    [ProducesResponseType(typeof(ResearchGapReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResearchGapReportDto>> IdentifyResearchGap([FromBody] IdentifyResearchGapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await analysisService.IdentifyResearchGapAsync(new IdentifyResearchGapCommand(request.Topic, User.GetActorName()), cancellationToken);
        return Ok(result.ToDto());
    }

    [HttpGet("research-gap/{reportId:guid}/stream")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task StreamResearchGapReport(Guid reportId, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";
        Response.StatusCode = 200;

        try
        {
            var result = await analysisService.IdentifyResearchGapAsync(
                new IdentifyResearchGapCommand(reportId.ToString(), User.GetActorName()), cancellationToken);

            var reportText = result.GapAnalysis?.ToJsonString() ?? "Report not available.";

            var words = reportText.Split(' ');
            foreach (var word in words)
            {
                if (cancellationToken.IsCancellationRequested) break;
                var bytes = Encoding.UTF8.GetBytes($"data: {word} ");
                await Response.Body.WriteAsync(bytes, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
                await Task.Delay(15, cancellationToken);
            }

            var endBytes = Encoding.UTF8.GetBytes("data: [DONE]\n\n");
            await Response.Body.WriteAsync(endBytes, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming research gap report");
            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
            }
        }
    }
}
