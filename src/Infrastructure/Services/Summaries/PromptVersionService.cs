using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Services.Summaries;

public sealed class PromptVersionService : IPromptVersionService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public PromptVersionService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<PromptVersionModel> CreateAsync(CreatePromptVersionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = new PromptVersion
        {
            Id = Guid.CreateVersion7(),
            Name = command.Name,
            Version = command.Version,
            SystemPrompt = command.SystemPrompt,
            UserPromptTemplate = command.UserPromptTemplate,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        dbContext.Set<PromptVersion>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToModel(entity);
    }

    public async Task<PromptVersionModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<PromptVersion>().FindAsync([id], cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<IReadOnlyList<PromptVersionModel>> ListAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<PromptVersion>()
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
        return entities.Select(ToModel).ToList();
    }

    public async Task<PromptVersionModel?> GetActiveVersionAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<PromptVersion>()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToModel(entity);
    }

    public async Task<PromptVersionModel> SetActiveAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var versions = await dbContext.Set<PromptVersion>().ToListAsync(cancellationToken);
        foreach (var version in versions)
        {
            version.IsActive = version.Id == id;
        }

        var entity = versions.FirstOrDefault(v => v.Id == id)
            ?? throw new NotFoundException(nameof(PromptVersion), id);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToModel(entity);
    }

    public async Task<PromptVersionModel> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entity = await dbContext.Set<PromptVersion>().FindAsync([id], cancellationToken)
            ?? throw new NotFoundException(nameof(PromptVersion), id);

        entity.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToModel(entity);
    }

    private static PromptVersionModel ToModel(PromptVersion entity) => new(
        entity.Id,
        entity.Name,
        entity.Version,
        entity.SystemPrompt,
        entity.UserPromptTemplate,
        entity.CreatedAt,
        entity.IsActive);
}