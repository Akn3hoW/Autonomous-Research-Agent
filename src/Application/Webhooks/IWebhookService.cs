namespace AutonomousResearchAgent.Application.Webhooks;

public interface IWebhookService
{
    Task<IReadOnlyCollection<WebhookListItem>> ListAsync(Guid userId, CancellationToken cancellationToken);
    Task<WebhookDetail> CreateAsync(CreateWebhookCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);
    Task<bool> DeliverAsync(WebhookPayload payload, CancellationToken cancellationToken);
}
