namespace AutonomousResearchAgent.Infrastructure.Services;

public sealed class SearchWeightsOptions
{
    public const string SectionName = "SearchWeights";
    public double Title { get; set; } = 1.0;
    public double Abstract { get; set; } = 0.6;
    public double Summary { get; set; } = 0.4;
    public double Document { get; set; } = 0.5;
    public int RrfConstantK { get; set; } = 60;
}
