using AutonomousResearchAgent.Api.Hubs;
using AutonomousResearchAgent.Application.Jobs;
using Microsoft.AspNetCore.SignalR;

namespace AutonomousResearchAgent.Api.Services;

public sealed class JobNotificationService(
    IHubContext<JobStatusHub> hubContext,
    ILogger<JobNotificationService> logger) : IJobNotificationService
{
    public async Task NotifyJobStatusChangedAsync(Guid jobId, string status, string? jobType = null, string? message = null)
    {
        try
        {
            var payload = new
            {
                jobId = jobId.ToString(),
                status,
                jobType,
                message,
                timestamp = DateTimeOffset.UtcNow
            };

            await hubContext.NotifyJobStatusChanged(jobId.ToString(), payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send JobStatusChanged notification for job {JobId}", jobId);
        }
    }

    public async Task NotifyJobCompletedAsync(Guid jobId, string status, string? resultJson = null, string? message = null)
    {
        try
        {
            var payload = new
            {
                jobId = jobId.ToString(),
                status,
                result = resultJson,
                message,
                timestamp = DateTimeOffset.UtcNow
            };

            await hubContext.NotifyJobCompleted(jobId.ToString(), payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send JobCompleted notification for job {JobId}", jobId);
        }
    }

    public async Task NotifyJobFailedAsync(Guid jobId, string status, string? errorMessage = null, int? retryCount = null, string? message = null)
    {
        try
        {
            var payload = new
            {
                jobId = jobId.ToString(),
                status,
                errorMessage,
                retryCount,
                message,
                timestamp = DateTimeOffset.UtcNow
            };

            await hubContext.NotifyJobFailed(jobId.ToString(), payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send JobFailed notification for job {JobId}", jobId);
        }
    }
}
