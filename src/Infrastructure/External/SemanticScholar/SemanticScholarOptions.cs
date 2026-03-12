namespace AutonomousResearchAgent.Infrastructure.External.SemanticScholar;

public sealed class SemanticScholarOptions
{
    public const string SectionName = "SemanticScholar";

    public string BaseUrl { get; set; } = "https://api.semanticscholar.org";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string Fields { get; set; } = "paperId,title,abstract,authors,year,venue,citationCount,externalIds";
}

