using AutonomousResearchAgent.Domain.Entities;

namespace AutonomousResearchAgent.Application.Summaries;

public interface IPromptVersionService
{
    Task<PromptVersionModel> CreateAsync(CreatePromptVersionCommand command, CancellationToken cancellationToken);
    Task<PromptVersionModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PromptVersionModel>> ListAsync(CancellationToken cancellationToken);
    Task<PromptVersionModel?> GetActiveVersionAsync(CancellationToken cancellationToken);
    Task<PromptVersionModel> SetActiveAsync(Guid id, CancellationToken cancellationToken);
    Task<PromptVersionModel> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record PromptVersionModel(
    Guid Id,
    string Name,
    string Version,
    string SystemPrompt,
    string UserPromptTemplate,
    DateTimeOffset CreatedAt,
    bool IsActive);

public sealed record CreatePromptVersionCommand(
    string Name,
    string Version,
    string SystemPrompt,
    string UserPromptTemplate);

public sealed record UpdatePromptVersionCommand(
    string? Name,
    string? SystemPrompt,
    string? UserPromptTemplate,
    bool? IsActive);