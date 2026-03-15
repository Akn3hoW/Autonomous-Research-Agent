using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutonomousResearchAgent.Infrastructure.BackgroundJobs;

public sealed class DatabaseJobWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BackgroundJobOptions> options,
    ILogger<DatabaseJobWorker> logger) : BackgroundService
{
    private readonly BackgroundJobOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var jobRunner = scope.ServiceProvider.GetRequiredService<IJobRunner>();

                var job = await ClaimNextQueuedJobAsync(dbContext, stoppingToken);
                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                    continue;
                }

                try
                {
                    await jobRunner.RunAsync(job, stoppingToken);
                    if (job.Status == JobStatus.Running)
                    {
                        job.Status = JobStatus.Completed;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background job {JobId} failed", job.Id);
                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = ex.Message;
                }

                await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Database job worker loop failed.");
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
        }
    }

    private static readonly string QueuedStatusLiteral = JobStatus.Queued.ToString();

    private static async Task<Job?> ClaimNextQueuedJobAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (dbContext.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var job = await dbContext.Jobs
                .FromSqlRaw(@"SELECT *
FROM jobs
WHERE ""Id"" = (
    SELECT ""Id""
    FROM jobs
    WHERE ""Status"" = {0}
    ORDER BY ""CreatedAt""
    FOR UPDATE SKIP LOCKED
    LIMIT 1)", QueuedStatusLiteral)
                .FirstOrDefaultAsync(cancellationToken);

            if (job is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            job.Status = JobStatus.Running;
            job.ErrorMessage = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return job;
        }

        var fallbackJob = await dbContext.Jobs
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(j => j.Status == JobStatus.Queued, cancellationToken);

        if (fallbackJob is null)
        {
            return null;
        }

        fallbackJob.Status = JobStatus.Running;
        fallbackJob.ErrorMessage = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return fallbackJob;
    }
}
