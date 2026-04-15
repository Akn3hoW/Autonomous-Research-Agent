namespace AutonomousResearchAgent.Application.Webhooks;

public interface IWebhookService
{
    Task<IReadOnlyCollection<WebhookListItem>> ListAsync(int userId, CancellationToken cancellationToken);
    Task<WebhookDetail> CreateAsync(CreateWebhookCommand command, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, int userId, CancellationToken cancellationToken);
    Task<bool> DeliverAsync(WebhookPayload payload, CancellationToken cancellationToken);
}
