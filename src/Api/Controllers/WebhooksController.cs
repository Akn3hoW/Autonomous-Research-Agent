using AutonomousResearchAgent.Api.Authorization;
using AutonomousResearchAgent.Api.Extensions;
using AutonomousResearchAgent.Application.Webhooks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousResearchAgent.Api.Controllers;

[ApiController]
[Route($"{ApiConstants.ApiPrefix}/me/webhooks")]
public sealed class WebhooksController(IWebhookService webhookService) : ControllerBase
{
    private static readonly HashSet<string> ValidEvents = ["job_completed", "saved_search_hit", "hypothesis_updated"];

    [HttpGet]
    [Authorize(Policy = PolicyNames.ReadAccess)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WebhookResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<WebhookResponse>>> GetWebhooks(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var webhooks = await webhookService.ListAsync(userId, cancellationToken);
        return Ok(webhooks.Select(w => new WebhookResponse(
            w.Id,
            w.Url,
            w.Events,
            w.IsActive,
            w.CreatedAt)).ToList());
    }

    [HttpPost]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(typeof(WebhookCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WebhookCreatedResponse>> CreateWebhook(
        [FromBody] CreateWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        if (request.Events.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Detail = "At least one event must be specified."
            });
        }

        var invalidEvents = request.Events.Where(e => !ValidEvents.Contains(e)).ToList();
        if (invalidEvents.Count > 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Detail = $"Invalid events: {string.Join(", ", invalidEvents)}. Valid events are: {string.Join(", ", ValidEvents)}"
            });
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Validation error",
                Detail = "A valid HTTP/HTTPS URL must be provided."
            });
        }

        var command = new CreateWebhookCommand(userId, request.Url, request.Events);
        var webhook = await webhookService.CreateAsync(command, cancellationToken);

        return CreatedAtAction(nameof(GetWebhooks), new WebhookCreatedResponse(
            webhook.Id,
            webhook.Url,
            webhook.Events,
            webhook.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = PolicyNames.EditAccess)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWebhook(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await webhookService.DeleteAsync(id, userId, cancellationToken);
        return NoContent();
    }

    private int? GetUserId() => User.GetUserId();
}

public sealed record WebhookResponse(
    Guid Id,
    string Url,
    IReadOnlyCollection<string> Events,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record WebhookCreatedResponse(
    Guid Id,
    string Url,
    IReadOnlyCollection<string> Events,
    DateTimeOffset CreatedAt);

public sealed record CreateWebhookRequest(
    string Url,
    IReadOnlyCollection<string> Events);
