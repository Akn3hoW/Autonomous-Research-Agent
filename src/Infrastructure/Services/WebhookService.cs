using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Webhooks;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class WebhookService(
    ApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhookService> logger) : IWebhookService
{
    public async Task<IReadOnlyCollection<WebhookListItem>> ListAsync(int userId, CancellationToken cancellationToken)
    {
        var webhooks = await dbContext.UserWebhooks
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WebhookListItem(
                w.Id,
                w.Url,
                w.Events,
                w.IsActive,
                w.CreatedAt))
            .ToListAsync(cancellationToken);

        return webhooks;
    }

    public async Task<WebhookDetail> CreateAsync(CreateWebhookCommand command, CancellationToken cancellationToken)
    {
        var secret = GenerateSecret();
        var webhook = new UserWebhook
        {
            UserId = command.UserId,
            Url = command.Url,
            Events = command.Events.ToList(),
            Secret = secret,
            IsActive = true
        };

        dbContext.UserWebhooks.Add(webhook);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created webhook {WebhookId} for user {UserId}", webhook.Id, command.UserId);

        return new WebhookDetail(
            webhook.Id,
            webhook.UserId,
            webhook.Url,
            webhook.Events,
            webhook.IsActive,
            webhook.CreatedAt);
    }

    public async Task DeleteAsync(Guid id, int userId, CancellationToken cancellationToken)
    {
        var webhook = await dbContext.UserWebhooks
            .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("Webhook", id);

        dbContext.UserWebhooks.Remove(webhook);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted webhook {WebhookId}", id);
    }

    public async Task<bool> DeliverAsync(WebhookPayload payload, CancellationToken cancellationToken)
    {
        var eventName = payload.Event;
        var webhooks = await dbContext.UserWebhooks
            .Where(w => w.IsActive && w.Events.Any(e => e == eventName))
            .ToListAsync(cancellationToken);

        if (webhooks.Count == 0)
            return true;

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var tasks = webhooks.Select(w => DeliverToWebhookAsync(w, json, cancellationToken));
        await Task.WhenAll(tasks);
        return true;
    }

    private async Task DeliverToWebhookAsync(UserWebhook webhook, string jsonPayload, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("Webhooks");
        var signature = ComputeHmacSignature(jsonPayload, webhook.Secret);
        const int maxRetries = 3;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("X-Webhook-Signature", signature);
                request.Headers.Add("X-Webhook-Id", webhook.Id.ToString());

                var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogDebug("Delivered webhook {WebhookId} to {Url}", webhook.Id, webhook.Url);
                    return;
                }

                logger.LogWarning("Webhook delivery failed for {WebhookId}: {StatusCode}", webhook.Id, response.StatusCode);

                if (attempt < maxRetries && IsRetryableStatusCode(response.StatusCode))
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                    logger.LogInformation("Retrying webhook {WebhookId} in {Delay}s (attempt {Attempt}/{MaxRetries})",
                        webhook.Id, delay.TotalSeconds, attempt + 1, maxRetries);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                return;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Webhook delivery attempt {Attempt} failed for {WebhookId}, retrying",
                    attempt + 1, webhook.Id);
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) * 2);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to deliver webhook {WebhookId} to {Url} after {MaxRetries} attempts",
                    webhook.Id, webhook.Url, maxRetries);
            }
        }
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.BadGateway ||
        statusCode == HttpStatusCode.ServiceUnavailable ||
        statusCode == HttpStatusCode.GatewayTimeout;

    private static string ComputeHmacSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return $"sha256={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string GenerateSecret()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}
