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
    private readonly JobRetryPolicy _retryPolicy = new();
    private static readonly ActivitySource ActivitySource = new("DatabaseJobWorker");
    private const int DefaultTimeoutSeconds = 3600;
    private const int CancellationCheckIntervalSeconds = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var jobRunner = scope.ServiceProvider.GetRequiredService<IJobRunner>();
                var notificationService = scope.ServiceProvider.GetService<IJobNotificationService>();

                var job = await ClaimNextQueuedJobAsync(dbContext, stoppingToken);
                if (job is null)
                {
                    await ProcessDueSavedSearchesAsync(dbContext, stoppingToken);
                    await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
                    continue;
                }

                await ExecuteJobWithRetryAsync(scope, dbContext, jobRunner, notificationService, job, stoppingToken);
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

    private async Task ExecuteJobWithRetryAsync(
        IServiceScope scope,
        ApplicationDbContext dbContext,
        IJobRunner jobRunner,
        IJobNotificationService? notificationService,
        Job job,
        CancellationToken stoppingToken)
    {
        var retryCount = job.RetryCount ?? 0;
        var maxAttempts = _retryPolicy.MaxAttemptsValue;
        var timeoutSeconds = job.TimeoutSeconds ?? DefaultTimeoutSeconds;

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var cancellationCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            timeoutCts.Token,
            cancellationCts.Token);

        var executionContext = new JobExecutionContext(job, linkedCts.Token);

        _ = Task.Run(async () =>
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                if (await CheckCancellationRequestedAsync(dbContext, job.Id, linkedCts.Token))
                {
                    cancellationCts.Cancel();
                    break;
                }
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(CancellationCheckIntervalSeconds), linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, linkedCts.Token);

        using var activity = ActivitySource.StartActivity("ProcessJob", ActivityKind.Internal);
        activity?.SetTag("job.id", job.Id.ToString());
        activity?.SetTag("job.type", job.Type.ToString());
        activity?.SetTag("job.retryCount", retryCount);

        try
        {
            await NotifyJobStatusChangedAsync(notificationService, job, JobStatus.Running, "Job started");

            await jobRunner.RunAsync(job, linkedCts.Token);

            if (job.Status == JobStatus.Running)
            {
                job.Status = JobStatus.Completed;
                await NotifyJobCompletedAsync(notificationService, dbContext, job, "Job completed successfully");
            }
        }
        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Job timed out");
            logger.LogWarning("Background job {JobId} timed out after {TimeoutSeconds} seconds", job.Id, timeoutSeconds);

            if (retryCount < maxAttempts && _retryPolicy.ShouldRetry(retryCount, new TimeoutException("Job timed out")))
            {
                await HandleRetryAsync(dbContext, job, retryCount, "Job timed out", maxAttempts, notificationService);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = $"Job timed out after {timeoutSeconds} seconds";
                await NotifyJobFailedAsync(notificationService, dbContext, job, "Job failed: timeout");
            }
        }
        catch (OperationCanceledException) when (cancellationCts.Token.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Job cancelled");
            logger.LogInformation("Background job {JobId} was cancelled", job.Id);
            job.Status = JobStatus.Cancelled;
            job.ErrorMessage = "Job was cancelled by user";
            await NotifyJobFailedAsync(notificationService, dbContext, job, "Job cancelled");
        }
        catch (OperationCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, "Job cancelled - shutting down");
            job.Status = JobStatus.Cancelled;
            job.ErrorMessage = "Job was cancelled due to system shutdown";
            await NotifyJobFailedAsync(notificationService, dbContext, job, "Job cancelled");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Background job {JobId} failed", job.Id);

            if (_retryPolicy.ShouldRetry(retryCount, ex))
            {
                await HandleRetryAsync(dbContext, job, retryCount, ex.Message, maxAttempts, notificationService);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
                await NotifyJobFailedAsync(notificationService, dbContext, job, $"Job failed: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private async Task<bool> CheckCancellationRequestedAsync(ApplicationDbContext dbContext, Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            var job = await dbContext.Jobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

            return job?.CancellationRequestedAt.HasValue == true;
        }
        catch
        {
            return false;
        }
    }

    private async Task HandleRetryAsync(
        ApplicationDbContext dbContext,
        Job job,
        int currentRetryCount,
        string errorMessage,
        int maxAttempts,
        IJobNotificationService? notificationService)
    {
        var nextRetryCount = currentRetryCount + 1;
        var delay = _retryPolicy.GetDelayForAttempt(nextRetryCount);

        job.RetryCount = nextRetryCount;
        job.LastAttemptAt = DateTimeOffset.UtcNow;
        job.Status = JobStatus.Queued;
        job.ErrorMessage = $"Retry {nextRetryCount}/{maxAttempts} after {delay.TotalSeconds:F0}s: {errorMessage}";
        job.RetryPolicyJson = _retryPolicy.Serialize();

        await dbContext.SaveChangesAsync(CancellationToken.None);

        logger.LogInformation("Job {JobId} scheduled for retry {RetryCount}/{MaxAttempts} after {DelaySeconds}s",
            job.Id, nextRetryCount, maxAttempts, delay.TotalSeconds);

        await NotifyJobStatusChangedAsync(notificationService, job, JobStatus.Queued, $"Retry scheduled: {nextRetryCount}/{maxAttempts}");

        await Task.Delay(delay, CancellationToken.None);
    }

    private async Task NotifyJobStatusChangedAsync(
        IJobNotificationService? notificationService,
        Job job,
        JobStatus status,
        string? message = null)
    {
        if (notificationService == null) return;

        try
        {
            await notificationService.NotifyJobStatusChangedAsync(
                job.Id,
                status.ToString(),
                job.Type.ToString(),
                message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send SignalR notification for job {JobId}", job.Id);
        }
    }

    private async Task NotifyJobCompletedAsync(
        IJobNotificationService? notificationService,
        ApplicationDbContext dbContext,
        Job job,
        string? message = null)
    {
        if (notificationService == null) return;

        try
        {
            await notificationService.NotifyJobCompletedAsync(
                job.Id,
                job.Status.ToString(),
                job.ResultJson,
                message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send SignalR notification for job {JobId}", job.Id);
        }
    }

    private async Task NotifyJobFailedAsync(
        IJobNotificationService? notificationService,
        ApplicationDbContext dbContext,
        Job job,
        string? message = null)
    {
        if (notificationService == null) return;

        try
        {
            await notificationService.NotifyJobFailedAsync(
                job.Id,
                job.Status.ToString(),
                job.ErrorMessage,
                job.RetryCount,
                message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send SignalR notification for job {JobId}", job.Id);
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
            job.CancellationRequestedAt = null;
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
                .SetProperty(p => p.ErrorMessage, (string?)null)
                .SetProperty(p => p.CancellationRequestedAt, (DateTimeOffset?)null),
                cancellationToken);

        if (updatedRows == 0)
        {
            return null;
        }

        fallbackJob.Status = JobStatus.Running;
        fallbackJob.ErrorMessage = null;
        fallbackJob.CancellationRequestedAt = null;
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

    private sealed class JobExecutionContext
    {
        public Job Job { get; }
        public CancellationToken CancellationToken { get; }

        public JobExecutionContext(Job job, CancellationToken cancellationToken)
        {
            Job = job;
            CancellationToken = cancellationToken;
        }
    }
}
