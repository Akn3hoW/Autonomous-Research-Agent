namespace AutonomousResearchAgent.Domain.Entities;

/// <summary>
/// Stores research trend analysis results over a specified time range.
/// </summary>
public sealed class TrendResult : AuditableEntity
{
    public string Field { get; set; } = string.Empty;
    public int StartYear { get; set; }
    public int EndYear { get; set; }

    /// <summary>
    /// JSON result from trend analysis. Schema:
    /// {
    ///   trends: { year: number, paperCount: number, avgCitations: number, topKeywords: string[] }[],
    ///   emergingTopics: { topic: string, growthRate: number, firstYear: number }[],
    ///   decliningTopics: { topic: string, declineRate: number, lastPeakYear: number }[],
    ///   summary: string
    /// }
    /// </summary>
    public string? ResultJson { get; set; }

    public DateTimeOffset? CalculatedAt { get; set; }
}