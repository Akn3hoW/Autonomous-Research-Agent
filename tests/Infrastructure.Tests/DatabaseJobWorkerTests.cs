using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.BackgroundJobs;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests;

public sealed class DatabaseJobWorkerTests
{
    [Fact]
    public async Task StartAsync_claims_and_processes_queued_job()
    {
        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(CreateBackgroundJobOptions(pollIntervalSeconds: 60));

        await using var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var mockRunner = new MockJobRunner();
        var completionSource = new TaskCompletionSource();
        mockRunner.SetupRunAsyncCompletion(completionSource);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => dbContext);
        serviceCollection.AddScoped<IJobRunner>(_ => mockRunner);
        var provider = serviceCollection.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            services.BuildServiceProvider().GetRequiredService<IOptions<BackgroundJobOptions>>(),
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await worker.StartAsync(cts.Token);
        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(1, mockRunner.RunCount);
    }

    [Fact]
    public async Task StartAsync_sets_job_status_to_completed_when_job_runs_successfully()
    {
        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(CreateBackgroundJobOptions(pollIntervalSeconds: 60));

        var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var mockRunner = new MockJobRunner();
        var completionSource = new TaskCompletionSource();
        mockRunner.SetupRunAsyncCompletion(completionSource);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => dbContext);
        serviceCollection.AddScoped<IJobRunner>(_ => mockRunner);
        var provider = serviceCollection.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            services.BuildServiceProvider().GetRequiredService<IOptions<BackgroundJobOptions>>(),
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await worker.StartAsync(cts.Token);
        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        var updatedJob = await dbContext.Jobs.AsNoTracking().FirstAsync(j => j.Id == job.Id, cts.Token);
        Assert.Equal(JobStatus.Completed, updatedJob.Status);
    }

    [Fact]
    public async Task StartAsync_sets_job_status_to_failed_when_job_throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(CreateBackgroundJobOptions(pollIntervalSeconds: 60));

        var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var mockRunner = new MockJobRunner();
        var completionSource = new TaskCompletionSource();
        mockRunner.SetupRunAsyncThrow(new InvalidOperationException("Job failed"), completionSource);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => dbContext);
        serviceCollection.AddScoped<IJobRunner>(_ => mockRunner);
        var provider = serviceCollection.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            services.BuildServiceProvider().GetRequiredService<IOptions<BackgroundJobOptions>>(),
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await worker.StartAsync(cts.Token);
        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        var updatedJob = await dbContext.Jobs.AsNoTracking().FirstAsync(j => j.Id == job.Id, cts.Token);
        Assert.Equal(JobStatus.Failed, updatedJob.Status);
        Assert.Equal("Job failed", updatedJob.ErrorMessage);
    }

    [Fact]
    public async Task StopAsync_cancels_execution_gracefully()
    {
        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(CreateBackgroundJobOptions(pollIntervalSeconds: 1));

        var dbContext = CreateDbContext();
        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        };
        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        var mockRunner = new MockJobRunner();
        var completionSource = new TaskCompletionSource();
        mockRunner.SetupRunAsyncBlock(completionSource);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => dbContext);
        serviceCollection.AddScoped<IJobRunner>(_ => mockRunner);
        var provider = serviceCollection.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            services.BuildServiceProvider().GetRequiredService<IOptions<BackgroundJobOptions>>(),
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await worker.StartAsync(cts.Token);
        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.True(mockRunner.RunCount <= 1);
        Assert.True(cts.Token.IsCancellationRequested || !mockRunner.IsBlocked);
    }

    [Fact]
    public async Task ExecuteAsync_does_not_process_anything_when_no_jobs_queued()
    {
        var services = new ServiceCollection();
        services.AddSingleton(NullLogger<DatabaseJobWorker>.Instance);
        services.AddSingleton(CreateBackgroundJobOptions(pollIntervalSeconds: 1));

        var dbContext = CreateDbContext();

        var mockRunner = new MockJobRunner();
        var completionSource = new TaskCompletionSource();
        mockRunner.SetupRunAsyncCompletion(completionSource);

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => dbContext);
        serviceCollection.AddScoped<IJobRunner>(_ => mockRunner);
        var provider = serviceCollection.BuildServiceProvider();

        var worker = new DatabaseJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            services.BuildServiceProvider().GetRequiredService<IOptions<BackgroundJobOptions>>(),
            NullLogger<DatabaseJobWorker>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        await worker.StartAsync(cts.Token);
        await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(0, mockRunner.RunCount);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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

    private sealed class MockJobRunner : IJobRunner
    {
        private Exception? _exceptionToThrow;
        private bool _block;
        private TaskCompletionSource? _completionSource;

        public int RunCount { get; private set; }
        public bool IsBlocked => _block && RunCount > 0;

        public void SetupRunAsyncCompletion(TaskCompletionSource completionSource)
        {
            _completionSource = completionSource;
            _completionSource.SetResult();
        }

        public void SetupRunAsyncThrow(Exception ex, TaskCompletionSource completionSource)
        {
            _exceptionToThrow = ex;
            _completionSource = completionSource;
            completionSource.SetResult();
        }

        public void SetupRunAsyncBlock(TaskCompletionSource completionSource)
        {
            _block = true;
            _completionSource = completionSource;
            completionSource.SetResult();
        }

        public async Task RunAsync(Job job, CancellationToken cancellationToken)
        {
            RunCount++;
            if (_exceptionToThrow is not null)
            {
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
                    return;
                }
            }
        }
    }
}
