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
        var entity = new Job
        {
            Type = command.Type,
            Status = JobStatus.Queued,
            PayloadJson = JsonNodeMapper.Serialize(command.Payload) ?? "{}",
            TargetEntityId = command.TargetEntityId,
            CreatedBy = command.CreatedBy
        };

        dbContext.Jobs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created job {JobId} of type {JobType}", entity.Id, entity.Type);
        return entity.ToModel();
    }

    public async Task<JobModel> RetryAsync(Guid id, RetryJobCommand command, CancellationToken cancellationToken)
    {
        var entity = await dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(Job), id);

        if (entity.Status is not (JobStatus.Failed or JobStatus.Cancelled))
        {
            throw new InvalidStateException("Only failed or cancelled jobs can be retried.");
        }

        entity.Status = JobStatus.Queued;
        entity.ErrorMessage = null;
        entity.ResultJson = null;
        entity.CreatedBy = command.RequestedBy ?? entity.CreatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Retried job {JobId}", entity.Id);

        return entity.ToModel();
    }
}
