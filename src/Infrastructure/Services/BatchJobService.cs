using AutonomousResearchAgent.Application.BatchJobs;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class BatchJobService(
    ApplicationDbContext dbContext,
    ILogger<BatchJobService> logger) : IBatchJobService
{
    public async Task<BatchJobModel> CreateAsync(CreateBatchJobCommand command, CancellationToken cancellationToken)
    {
        var entity = new BatchJob
        {
            Action = command.Action,
            UserId = command.UserId,
            Total = command.Total,
            Completed = 0,
            Status = BatchJobStatus.Pending
        };

        dbContext.BatchJobs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created batch job {BatchJobId} for action {Action} with {Total} items", entity.Id, entity.Action, entity.Total);
        return entity.ToModel();
    }

    public async Task<BatchJobModel> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BatchJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(BatchJob), id);

        return entity.ToModel();
    }

    public async Task<BatchJobModel> UpdateProgressAsync(Guid id, int completed, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BatchJobs.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(BatchJob), id);

        if (completed > entity.Total)
        {
            throw new ValidationException($"Completed count ({completed}) cannot exceed total count ({entity.Total}).");
        }

        entity.Completed = completed;
        if (entity.Status == BatchJobStatus.Pending && completed > 0)
        {
            entity.Status = BatchJobStatus.Processing;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToModel();
    }

    public async Task<BatchJobModel> CompleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BatchJobs.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(BatchJob), id);

        entity.Status = BatchJobStatus.Completed;
        entity.Completed = entity.Total;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Completed batch job {BatchJobId}", entity.Id);
        return entity.ToModel();
    }

    public async Task<BatchJobModel> FailAsync(Guid id, string error, CancellationToken cancellationToken)
    {
        var entity = await dbContext.BatchJobs.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(BatchJob), id);

        entity.Status = BatchJobStatus.Failed;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogError("Batch job {BatchJobId} failed: {Error}", entity.Id, error);
        return entity.ToModel();
    }
}

file static class BatchJobExtensions
{
    public static BatchJobModel ToModel(this BatchJob entity) =>
        new(entity.Id, entity.UserId, entity.Action, entity.Status.ToString(), entity.Total, entity.Completed, entity.CreatedAt, entity.UpdatedAt);
}