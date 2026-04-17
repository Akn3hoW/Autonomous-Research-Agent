using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.BackgroundJobs;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Infrastructure.Tests;

public sealed class JobRetryLogicTests
{
    [Fact]
    public async Task RetryAsync_resets_failed_job_to_queued()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Failed,
            PayloadJson = "{}",
            ErrorMessage = "Previous failure"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        var result = await service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None);

        Assert.Equal(JobStatus.Queued, result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Result);
        Assert.Equal(0, result.RetryCount);
    }

    [Fact]
    public async Task RetryAsync_resets_cancelled_job_to_queued()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Cancelled,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        var result = await service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None);

        Assert.Equal(JobStatus.Queued, result.Status);
    }

    [Fact]
    public async Task RetryAsync_throws_when_job_not_found()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateJobService(dbContext);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.RetryAsync(Guid.NewGuid(), new RetryJobCommand(null, null), CancellationToken.None));
    }

    [Fact]
    public async Task RetryAsync_throws_when_job_status_is_not_failed_or_cancelled()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Completed,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None));
    }

    [Fact]
    public async Task RetryAsync_throws_when_job_status_is_running()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Running,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None));
    }

    [Fact]
    public async Task RetryAsync_throws_when_job_status_is_queued()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        await Assert.ThrowsAsync<InvalidStateException>(() =>
            service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None));
    }

    [Fact]
    public async Task RetryAsync_updates_created_by_when_requested_by_is_provided()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Failed,
            PayloadJson = "{}",
            CreatedBy = "original-user"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        var result = await service.RetryAsync(job.Id, new RetryJobCommand("new-user", null), CancellationToken.None);

        Assert.Equal("new-user", result.CreatedBy);
    }

    [Fact]
    public async Task RetryAsync_preserves_created_by_when_requested_by_is_null()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Failed,
            PayloadJson = "{}",
            CreatedBy = "original-user"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        var result = await service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None);

        Assert.Equal("original-user", result.CreatedBy);
    }

    [Fact]
    public async Task RetryAsync_clears_error_message_and_result()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Failed,
            PayloadJson = "{}",
            ErrorMessage = "Some error",
            ResultJson = "{\"key\":\"value\"}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        var result = await service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None);

        Assert.Null(result.ErrorMessage);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task RetryAsync_clears_retry_count_and_retry_policy()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Failed,
            PayloadJson = "{}",
            RetryCount = 3,
            RetryPolicyJson = "{\"attemptNumber\":3}",
            LastAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        var result = await service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None);

        Assert.Equal(0, result.RetryCount);
        Assert.Null(result.RetryPolicy);
        Assert.Null(result.LastAttemptAt);
    }

    [Fact]
    public async Task RetryAsync_logs_information()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Failed,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<JobService>>();
        var service = new JobService(dbContext, loggerMock.Object);

        await service.RetryAsync(job.Id, new RetryJobCommand(null, null), CancellationToken.None);

        loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Retried job")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void JobRetryPolicy_GetDelayForAttempt_returns_exponential_backoff()
    {
        var policy = new JobRetryPolicy();

        var delay0 = policy.GetDelayForAttempt(0);
        var delay1 = policy.GetDelayForAttempt(1);
        var delay2 = policy.GetDelayForAttempt(2);
        var delay3 = policy.GetDelayForAttempt(3);
        var delay4 = policy.GetDelayForAttempt(4);

        Assert.InRange(delay0, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(39));
        Assert.InRange(delay1, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(78));
        Assert.InRange(delay2, TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(156));
        Assert.InRange(delay3, TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(312));
        Assert.InRange(delay4, TimeSpan.FromSeconds(480), TimeSpan.FromSeconds(624));
    }

    [Fact]
    public void JobRetryPolicy_GetDelayForAttempt_respects_max_delay()
    {
        var policy = new JobRetryPolicy();

        var delay10 = policy.GetDelayForAttempt(10);

        Assert.Equal(TimeSpan.FromHours(1), delay10);
    }

    [Fact]
    public void JobRetryPolicy_ShouldRetry_returns_false_after_max_attempts()
    {
        var policy = new JobRetryPolicy();

        var result = policy.ShouldRetry(5, new HttpRequestException("Test"));

        Assert.False(result);
    }

    [Fact]
    public void JobRetryPolicy_ShouldRetry_returns_true_for_rate_limit()
    {
        var policy = new JobRetryPolicy();

        var result = policy.ShouldRetry(1, new HttpRequestException("Rate limit", null, System.Net.HttpStatusCode.TooManyRequests));

        Assert.True(result);
    }

    [Fact]
    public void JobRetryPolicy_ShouldRetry_returns_true_for_service_unavailable()
    {
        var policy = new JobRetryPolicy();

        var result = policy.ShouldRetry(1, new HttpRequestException("Service unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable));

        Assert.True(result);
    }

    [Fact]
    public void JobRetryPolicy_ShouldRetry_returns_false_for_operation_cancelled()
    {
        var policy = new JobRetryPolicy();

        var result = policy.ShouldRetry(1, new OperationCanceledException());

        Assert.False(result);
    }

    [Fact]
    public void JobRetryPolicy_IsRateLimitException_detects_429()
    {
        var policy = new JobRetryPolicy();

        var exception = new HttpRequestException("Rate limit", null, System.Net.HttpStatusCode.TooManyRequests);

        Assert.True(policy.IsRateLimitException(exception));
    }

    [Fact]
    public void JobRetryPolicy_IsServiceUnavailableException_detects_503()
    {
        var policy = new JobRetryPolicy();

        var exception = new HttpRequestException("Service unavailable", null, System.Net.HttpStatusCode.ServiceUnavailable);

        Assert.True(policy.IsServiceUnavailableException(exception));
    }

    [Fact]
    public void JobRetryPolicy_IsNetworkError_detects_network_exceptions()
    {
        var policy = new JobRetryPolicy();

        Assert.True(policy.IsNetworkError(new HttpRequestException("Network error")));
        Assert.True(policy.IsNetworkError(new TaskCanceledException()));
    }

    [Fact]
    public void JobRetryPolicy_CreateRetryPolicyData_creates_valid_data()
    {
        var policy = new JobRetryPolicy();
        var exception = new HttpRequestException("Rate limit", null, System.Net.HttpStatusCode.TooManyRequests);
        var delay = TimeSpan.FromSeconds(60);

        var result = policy.CreateRetryPolicyData(1, exception, delay);

        Assert.Equal(1, result.AttemptNumber);
        Assert.Equal("HttpRequestException", result.ExceptionType);
        Assert.Equal("Rate limit", result.ExceptionMessage);
        Assert.Equal(60, result.DelaySeconds);
        Assert.True(result.ShouldRetry);
        Assert.Contains("429", result.Reason);
    }

    [Fact]
    public async Task UpdateRetryStatusAsync_updates_retry_fields()
    {
        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);
        var retryPolicyJson = "{\"attemptNumber\":1,\"delaySeconds\":30}";

        var result = await service.UpdateRetryStatusAsync(job.Id, 1, retryPolicyJson, CancellationToken.None);

        Assert.Equal(1, result.RetryCount);
        Assert.NotNull(result.RetryPolicy);
        Assert.NotNull(result.LastAttemptAt);
    }

    [Fact]
    public async Task GetJobsByParentIdAsync_returns_child_jobs()
    {
        await using var dbContext = CreateDbContext();
        var parentJob = new Job
        {
            Type = JobType.ResearchGoal,
            Status = JobStatus.Running,
            PayloadJson = "{}"
        };
        var childJob1 = new Job
        {
            Type = JobType.SearchPapers,
            Status = JobStatus.Completed,
            PayloadJson = "{}",
            ParentJobId = parentJob.Id,
            WorkflowStep = 1
        };
        var childJob2 = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}",
            ParentJobId = parentJob.Id,
            WorkflowStep = 2
        };
        dbContext.Jobs.AddRange(parentJob, childJob1, childJob2);
        await dbContext.SaveChangesAsync();

        var service = CreateJobService(dbContext);

        var result = await service.GetJobsByParentIdAsync(parentJob.Id, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].WorkflowStep);
        Assert.Equal(2, result[1].WorkflowStep);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static JobService CreateJobService(ApplicationDbContext dbContext)
    {
        return new JobService(dbContext, NullLogger<JobService>.Instance);
    }
}
