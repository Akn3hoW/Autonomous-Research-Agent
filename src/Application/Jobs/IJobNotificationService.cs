namespace AutonomousResearchAgent.Application.Jobs;

public interface IJobNotificationService
{
    Task NotifyJobStatusChangedAsync(Guid jobId, string status, string? jobType = null, string? message = null);
    Task NotifyJobCompletedAsync(Guid jobId, string status, string? resultJson = null, string? message = null);
    Task NotifyJobFailedAsync(Guid jobId, string status, string? errorMessage = null, int? retryCount = null, string? message = null);
}
