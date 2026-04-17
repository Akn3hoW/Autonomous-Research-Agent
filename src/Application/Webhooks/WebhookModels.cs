namespace AutonomousResearchAgent.Application.Webhooks;

public sealed record WebhookListItem(
    Guid Id,
    string Url,
    IReadOnlyCollection<string> Events,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record WebhookDetail(
    Guid Id,
    Guid UserId,
    string Url,
    IReadOnlyCollection<string> Events,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateWebhookCommand(
    Guid UserId,
    string Url,
    IReadOnlyCollection<string> Events);

public sealed record WebhookPayload(
    string Event,
    Guid WebhookId,
    DateTimeOffset Timestamp,
    object Data);
