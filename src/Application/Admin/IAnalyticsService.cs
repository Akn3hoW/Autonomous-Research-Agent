namespace AutonomousResearchAgent.Application.Admin;

public interface IAnalyticsService
{
    Task<AnalyticsDto> GetAnalyticsAsync(CancellationToken cancellationToken = default);
}

public sealed record AnalyticsDto(
    int TotalPapers,
    IReadOnlyList<PaperOverTimeDto> PapersOverTime,
    IReadOnlyList<PaperSourceCountDto> PapersBySource,
    IReadOnlyList<PaperStatusCountDto> PapersByStatus,
    IReadOnlyList<PaperVenueCountDto> PapersByVenue,
    IReadOnlyList<PaperYearCountDto> PapersByYear,
    IReadOnlyList<JobThroughputDto> JobThroughput,
    IReadOnlyList<SearchQueryVolumeDto> SearchQueryVolume,
    long AverageProcessingTimeMs,
    IReadOnlyList<TopKeywordDto> TopKeywords);

public sealed record PaperOverTimeDto(string Month, int Count);

public sealed record PaperSourceCountDto(string Source, int Count);

public sealed record PaperStatusCountDto(string Status, int Count);

public sealed record PaperVenueCountDto(string Venue, int Count);

public sealed record PaperYearCountDto(int Year, int Count);

public sealed record JobThroughputDto(string Month, int Completed, int Failed, int Pending);

public sealed record SearchQueryVolumeDto(string Day, int Count);

public sealed record TopKeywordDto(string Keyword, int Count);
