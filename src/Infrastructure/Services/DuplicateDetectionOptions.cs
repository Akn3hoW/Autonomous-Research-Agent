namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class DuplicateDetectionOptions
{
    public const string SectionName = "DuplicateDetection";

    public int TopK { get; set; } = 50;
    public int MaxComparisonsPerBatch { get; set; } = 10000;
}