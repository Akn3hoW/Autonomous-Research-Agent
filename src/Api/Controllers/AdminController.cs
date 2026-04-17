using System.Text.Json;
using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Admin;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Admin;
using AutonomousResearchAgent.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/admin")]
public sealed class AdminController(
    IAuditService auditService,
    IAnalyticsService analyticsService) : ControllerBase
{
    [HttpGet("audit-log")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(AuditLogResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogResponse>> GetAuditLog([FromQuery] AuditLogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new AuditLogQuery(
            request.PageNumber,
            request.PageSize,
            request.UserId,
            request.EntityType,
            request.Action,
            request.StartDate,
            request.EndDate);

        var result = await auditService.GetAuditLogAsync(query, cancellationToken);

        var items = result.Items.Select(e => new AuditEventDto(
            e.Id,
            e.UserId,
            e.UserName,
            e.EntityType,
            e.EntityId,
            e.Action,
            string.IsNullOrWhiteSpace(e.DiffJson) ? null : TryParseJson(e.DiffJson),
            e.Timestamp)).ToList();

        return Ok(new AuditLogResponse(items, result.PageNumber, result.PageSize, result.TotalCount));
    }

    [HttpGet("analytics")]
    [Authorize(Policy = PolicyNames.AdminAccess)]
    [ProducesResponseType(typeof(AnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnalyticsDto>> GetAnalytics(CancellationToken cancellationToken)
    {
        var analytics = await analyticsService.GetAnalyticsAsync(cancellationToken);
        return Ok(analytics);
    }

    private static object? TryParseJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<object>(json);
        }
        catch
        {
            return json;
        }
    }
}