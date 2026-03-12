namespace AutonomousResearchAgent.Infrastructure.Services;

internal static class QueryHelpers
{
    public static string ToILikePattern(string value) => $"%{value.Trim()}%";
}
