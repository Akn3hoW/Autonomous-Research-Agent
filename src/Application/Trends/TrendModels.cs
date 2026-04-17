namespace AutonomousResearchAgent.Application.Trends;

public sealed record TrendsRequest(
    string? Field,
    int StartYear,
    int EndYear);

public sealed record TrendBucket(
    int Year,
    List<TrendTopic> Topics);

public sealed record TrendTopic(
    string Topic,
    double Momentum,
    int PaperCount,
    List<string> SamplePapers);

public sealed record TrendsResponse(
    List<TrendBucket> Buckets,
    List<string> EmergingThemes,
    List<string> DecliningThemes);