using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Watchlist;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Api.Middleware;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Watchlist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(PagedResponse<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<NotificationDto>>> GetNotifications(
        [FromQuery] NotificationQueryRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await notificationService.ListAsync(request.ToApplicationModel(userId), cancellationToken);
        return Ok(result.ToPagedResponse(item => item.ToDto()));
    }

    [HttpGet("unread-count")]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(UnreadCountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var count = await notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(new UnreadCountResponse(count));
    }

    [HttpPut("{id:guid}/read")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NotificationDto>> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var notification = await notificationService.MarkAsReadAsync(id, cancellationToken);
        return Ok(notification.ToDto());
    }

    [HttpPut("read-all")]
    [Audited]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(MarkAllReadResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MarkAllReadResponse>> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var count = await notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(new MarkAllReadResponse(count));
    }

    private Guid GetUserId()
    {
        var userId = User.GetUserGuid();
        return userId ?? throw new AuthenticationException("User ID not found in token.");
    }
}
