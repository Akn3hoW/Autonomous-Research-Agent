namespace AutonomousResearchAgent.Api.Contracts.Trends;

public sealed class TrendsRequest
{
    public string? Field { get; init; }
    public int StartYear { get; init; }
    public int EndYear { get; init; }
}

public sealed record TrendBucketDto(
    int Year,
    IReadOnlyList<TrendTopicDto> Topics);

public sealed record TrendTopicDto(
    string Topic,
    double Momentum,
    int PaperCount,
    IReadOnlyList<string> SamplePapers);

public sealed record TrendsResponseDto(
    IReadOnlyList<TrendBucketDto> Buckets,
    IReadOnlyList<string> EmergingThemes,
    IReadOnlyList<string> DecliningThemes);