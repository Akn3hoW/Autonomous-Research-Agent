using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
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
