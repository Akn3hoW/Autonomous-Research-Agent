namespace AutonomousResearchAgent.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public int PollIntervalSeconds { get; set; } = 5;
}
