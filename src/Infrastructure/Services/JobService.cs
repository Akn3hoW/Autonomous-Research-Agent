using System;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class JobService(
    ApplicationDbContext dbContext,
    ILogger<JobService> logger) : IJobService
{
    public async Task<PagedResult<JobModel>> ListAsync(JobQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var jobs = dbContext.Jobs.AsNoTracking().AsQueryable();

        if (query.Type.HasValue)
        {
            jobs = jobs.Where(j => j.Type == query.Type.Value);
        }

        if (query.Status.HasValue)
        {
            jobs = jobs.Where(j => j.Status == query.Status.Value);
        }

        var totalCount = await jobs.LongCountAsync(cancellationToken);
        var entities = await jobs
            .OrderByDescending(j => j.CreatedAt)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        var items = entities.Select(j => j.ToModel()).ToList();

        return new PagedResult<JobModel>(items, query.PageNumber, query.PageSize, totalCount);
    }

    public async Task<JobModel> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), id);

        return job.ToModel();
    }

    public async Task<JobModel> CreateAsync(CreateJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = new Job
        {
            Type = command.Type,
            Status = JobStatus.Queued,
            PayloadJson = JsonNodeMapper.Serialize(command.Payload) ?? "{}",
            TargetEntityId = command.TargetEntityId,
            CreatedBy = command.CreatedBy,
            ParentJobId = command.ParentJobId,
            DependsOnJobIds = command.DependsOnJobIds != null
                ? System.Text.Json.JsonSerializer.Serialize(command.DependsOnJobIds)
                : null,
            WorkflowStep = command.WorkflowStep
        };

        dbContext.Jobs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created job {JobId} of type {JobType}", entity.Id, entity.Type);
        return entity.ToModel();
    }

    public async Task<JobModel> RetryAsync(Guid id, RetryJobCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entity = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), id);

        if (entity.Status is not (JobStatus.Failed or JobStatus.Cancelled))
        {
            throw new InvalidStateException("Only failed or cancelled jobs can be retried.");
        }

        entity.Status = JobStatus.Queued;
        entity.ErrorMessage = null;
        entity.ResultJson = null;
        entity.RetryCount = 0;
        entity.RetryPolicyJson = null;
        entity.LastAttemptAt = null;
        entity.CreatedBy = command.RequestedBy ?? entity.CreatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Retried job {JobId}", entity.Id);

        return entity.ToModel();
    }

    public async Task<JobModel> UpdateRetryStatusAsync(Guid id, int retryCount, string retryPolicyJson, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), id);

        entity.RetryCount = retryCount;
        entity.RetryPolicyJson = retryPolicyJson;
        entity.LastAttemptAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToModel();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), id);

        dbContext.Jobs.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted job {JobId}", id);
    }

    public async Task<List<JobModel>> GetJobsByParentIdAsync(Guid parentId, CancellationToken cancellationToken)
    {
        var entities = await dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.ParentJobId == parentId)
            .OrderBy(j => j.WorkflowStep)
            .ThenBy(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(j => j.ToModel()).ToList();
    }

    public async Task<JobModel> UpdateStatusAsync(Guid id, JobStatus status, string? errorMessage, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), id);

        entity.Status = status;
        if (errorMessage != null)
        {
            entity.ErrorMessage = errorMessage;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToModel();
    }

    public async Task<JobModel> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), id);

        if (entity.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
        {
            throw new InvalidStateException("Only queued or running jobs can be cancelled.");
        }

        entity.Status = JobStatus.Cancelled;
        entity.ErrorMessage = "Job was cancelled by user.";
        entity.CancellationRequestedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.ToModel();
    }
}
