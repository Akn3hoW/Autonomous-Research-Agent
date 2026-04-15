namespace AutonomousResearchAgent.Api.Authorization;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    public int ExpensivePermitLimit { get; set; } = 10;
    public int JobCreationPermitLimit { get; set; } = 20;
    public int StandardPermitLimit { get; set; } = 100;
    public int WindowSeconds { get; set; } = 60;
}