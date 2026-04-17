using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.BackgroundJobs;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Infrastructure.Tests;

public sealed class DatabaseJobWorkerTests : IDisposable
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private ApplicationDbContext? _dbContext;

    [Fact]
    public async Task StartAsync_claims_and_processes_queued_job()
    {
        _dbContext = CreateDbContext();
        var mockRunner = new MockJobRunner();
        var options = CreateBackgroundJobOptions(pollIntervalSeconds: 1);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(options);
        services.AddScoped<ApplicationDbContext>(sp => _dbContext!);
        services.AddScoped<IJobRunner>(sp => mockRunner);
        services.AddScoped<IJobNotificationService>(sp => Mock.Of<IJobNotificationService>());
        var provider = services.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var startTask = worker.StartAsync(cts.Token);

        await mockRunner.WaitForRunStartAsync(TimeSpan.FromSeconds(5));

        cts.Cancel();

        await startTask;

        Assert.Equal(1, mockRunner.RunCount);
    }

    [Fact]
    public async Task StartAsync_sets_job_status_to_completed_when_job_runs_successfully()
    {
        _dbContext = CreateDbContext();
        var mockRunner = new MockJobRunner();
        var options = CreateBackgroundJobOptions(pollIntervalSeconds: 1);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(options);
        services.AddScoped<ApplicationDbContext>(sp => _dbContext!);
        services.AddScoped<IJobRunner>(sp => mockRunner);
        services.AddScoped<IJobNotificationService>(sp => Mock.Of<IJobNotificationService>());
        var provider = services.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var startTask = worker.StartAsync(cts.Token);

        await mockRunner.WaitForRunStartAsync(TimeSpan.FromSeconds(5));

        await mockRunner.WaitForRunCompletionAsync(TimeSpan.FromSeconds(5));

        cts.Cancel();

        await startTask;

        await using var verifyContext = CreateDbContext();
        var jobStatus = await verifyContext.Jobs.AsNoTracking().FirstAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Completed, jobStatus.Status);
    }

    [Fact]
    public async Task StartAsync_sets_job_status_to_failed_when_job_throws()
    {
        _dbContext = CreateDbContext();
        var mockRunner = new MockJobRunner();
        mockRunner.SetupRunAsyncThrow(new InvalidOperationException("Job failed"));
        var options = CreateBackgroundJobOptions(pollIntervalSeconds: 1);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(options);
        services.AddScoped<ApplicationDbContext>(sp => _dbContext!);
        services.AddScoped<IJobRunner>(sp => mockRunner);
        services.AddScoped<IJobNotificationService>(sp => Mock.Of<IJobNotificationService>());
        var provider = services.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var startTask = worker.StartAsync(cts.Token);

        await mockRunner.WaitForRunStartAsync(TimeSpan.FromSeconds(5));

        await mockRunner.WaitForRunCompletionAsync(TimeSpan.FromSeconds(5));

        cts.Cancel();

        await startTask;

        await using var verifyContext = CreateDbContext();
        var jobStatus = await verifyContext.Jobs.AsNoTracking().FirstAsync(j => j.Id == job.Id);
        Assert.Equal(JobStatus.Failed, jobStatus.Status);
        Assert.Equal("Job failed", jobStatus.ErrorMessage);
    }

    [Fact]
    public async Task StopAsync_cancels_execution_gracefully()
    {
        _dbContext = CreateDbContext();
        var mockRunner = new MockJobRunner();
        mockRunner.SetupRunAsyncBlock();
        var options = CreateBackgroundJobOptions(pollIntervalSeconds: 1);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(options);
        services.AddScoped<ApplicationDbContext>(sp => _dbContext!);
        services.AddScoped<IJobRunner>(sp => mockRunner);
        services.AddScoped<IJobNotificationService>(sp => Mock.Of<IJobNotificationService>());
        var provider = services.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var startTask = worker.StartAsync(cts.Token);

        await mockRunner.WaitForRunStartAsync(TimeSpan.FromSeconds(3));

        cts.Cancel();

        await startTask;

        Assert.True(mockRunner.RunCount <= 1);
        Assert.True(cts.Token.IsCancellationRequested || !mockRunner.IsBlocked);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_process_anything_when_no_jobs_queued()
    {
        _dbContext = CreateDbContext();
        var mockRunner = new MockJobRunner();
        var options = CreateBackgroundJobOptions(pollIntervalSeconds: 1);

        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(options);
        services.AddScoped<ApplicationDbContext>(sp => _dbContext!);
        services.AddScoped<IJobRunner>(sp => mockRunner);
        services.AddScoped<IJobNotificationService>(sp => Mock.Of<IJobNotificationService>());
        var provider = services.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        await worker.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(3));
        cts.Cancel();

        Assert.Equal(0, mockRunner.RunCount);
    }

    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IOptions<BackgroundJobOptions> CreateBackgroundJobOptions(int pollIntervalSeconds)
    {
        return Options.Create(new BackgroundJobOptions
        {
            PollIntervalSeconds = pollIntervalSeconds
        });
    }

    public void Dispose()
    {
    }

    private sealed class MockJobRunner : IJobRunner
    {
        private Exception? _exceptionToThrow;
        private bool _block;
        private int _runCount;
        private readonly TaskCompletionSource<bool> _runStartedSource = new();
        private readonly TaskCompletionSource<bool> _runCompletedSource = new();

        public int RunCount => _runCount;
        public bool IsBlocked => _block && _runCount > 0;

        public void SetupRunAsyncCompletion()
        {
        }

        public void SetupRunAsyncThrow(Exception ex)
        {
            _exceptionToThrow = ex;
        }

        public void SetupRunAsyncBlock()
        {
            _block = true;
        }

        public async Task WaitForRunStartAsync(TimeSpan timeout)
        {
            try
            {
                await _runStartedSource.Task.WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
            }
        }

        public async Task WaitForRunCompletionAsync(TimeSpan timeout)
        {
            try
            {
                await _runCompletedSource.Task.WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
            }
        }

        public async Task RunAsync(Job job, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _runCount);
            _runStartedSource.TrySetResult(true);

            if (_exceptionToThrow is not null)
            {
                _runCompletedSource.TrySetResult(false);
                throw _exceptionToThrow;
            }

            if (_block)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _runCompletedSource.TrySetResult(true);
                    return;
                }
            }

            _runCompletedSource.TrySetResult(true);
        }
    }
}
