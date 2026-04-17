namespace AutonomousResearchAgent.Api.Contracts.Summaries;

public sealed record PromptVersionDto(
    Guid Id,
    string Name,
    string Version,
    string SystemPrompt,
    string UserPromptTemplate,
    DateTimeOffset CreatedAt,
    bool IsActive);

public sealed class CreatePromptVersionRequest
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string SystemPrompt { get; init; } = string.Empty;
    public string UserPromptTemplate { get; init; } = string.Empty;
}

public sealed class UpdatePromptVersionRequest
{
    public string? Name { get; init; }
    public string? SystemPrompt { get; init; }
    public string? UserPromptTemplate { get; init; }
    public bool? IsActive { get; init; }
}