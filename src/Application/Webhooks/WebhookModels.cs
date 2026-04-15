namespace AutonomousResearchAgent.Application.Webhooks;

public sealed record WebhookListItem(
    Guid Id,
    string Url,
    IReadOnlyCollection<string> Events,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record WebhookDetail(
    Guid Id,
    int UserId,
    string Url,
    IReadOnlyCollection<string> Events,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateWebhookCommand(
    int UserId,
    string Url,
    IReadOnlyCollection<string> Events);

public sealed record WebhookPayload(
    string Event,
    Guid WebhookId,
    DateTimeOffset Timestamp,
    object Data);
