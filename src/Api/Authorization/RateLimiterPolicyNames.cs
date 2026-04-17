namespace AutonomousResearchAgent.Api.Authorization;

public static class RateLimiterPolicyNames
{
    public const string Expensive = "expensive";
    public const string JobCreation = "jobCreation";
    public const string Standard = "standard";
    public const string Strict = "strict";
}