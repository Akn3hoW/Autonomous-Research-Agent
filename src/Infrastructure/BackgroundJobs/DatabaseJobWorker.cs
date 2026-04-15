using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private static readonly ActivitySource ActivitySource = new("DatabaseJobWorker");

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
                    await ProcessDueSavedSearchesAsync(dbContext, stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                    continue;
                }

                using var activity = ActivitySource.StartActivity("ProcessJob", ActivityKind.Internal);
                activity?.SetTag("job.id", job.Id.ToString());
                activity?.SetTag("job.type", job.Type.ToString());

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
                    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
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

        var updatedRows = await dbContext.Jobs
            .Where(j => j.Id == fallbackJob.Id && j.Status == JobStatus.Queued)
            .ExecuteUpdateAsync(j => j
                .SetProperty(p => p.Status, JobStatus.Running)
                .SetProperty(p => p.ErrorMessage, (string?)null),
                cancellationToken);

        if (updatedRows == 0)
        {
            return null;
        }

        fallbackJob.Status = JobStatus.Running;
        fallbackJob.ErrorMessage = null;
        return fallbackJob;
    }

    private async Task ProcessDueSavedSearchesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var dueSearches = await dbContext.SavedSearches
            .Where(s => s.IsActive && s.Schedule != ScheduleType.Manual)
            .ToListAsync(cancellationToken);

        foreach (var search in dueSearches)
        {
            if (IsDueForPolling(search, now))
            {
                var existingJob = await dbContext.Jobs
                    .AnyAsync(j => j.TargetEntityId == search.Id &&
                                   j.Type == JobType.WatchlistPolling &&
                                   j.Status == JobStatus.Queued, cancellationToken);

                if (!existingJob)
                {
                    var job = new Job
                    {
                        Type = JobType.WatchlistPolling,
                        Status = JobStatus.Queued,
                        TargetEntityId = search.Id,
                        PayloadJson = JsonNode.Parse(JsonSerializer.Serialize(new { savedSearchId = search.Id }))?.ToJsonString() ?? "{}",
                        CreatedBy = "system-watchlist"
                    };
                    dbContext.Jobs.Add(job);
                    logger.LogInformation("Created WatchlistPolling job for saved search {SavedSearchId}", search.Id);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsDueForPolling(SavedSearch search, DateTimeOffset now)
    {
        if (!search.LastRunAt.HasValue)
        {
            return true;
        }

        var lastRun = search.LastRunAt.Value;

        return search.Schedule switch
        {
            ScheduleType.Hourly => now - lastRun >= TimeSpan.FromHours(1),
            ScheduleType.Daily => now - lastRun >= TimeSpan.FromDays(1),
            ScheduleType.Weekly => now - lastRun >= TimeSpan.FromDays(7),
            _ => false
        };
    }
}
