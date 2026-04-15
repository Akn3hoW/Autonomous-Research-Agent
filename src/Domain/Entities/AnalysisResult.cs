using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

/// <summary>
/// Stores the results of AI-generated analysis including cross-paper insights and comparisons.
/// </summary>
public sealed class AnalysisResult : AuditableEntity
{
    public Guid? JobId { get; set; }
    public AnalysisType AnalysisType { get; set; }

    /// <summary>
    /// JSON object describing the input set used for analysis.
    /// Schema: { filter?: string, paperIds?: guid[], field?: string }
    /// </summary>
    public string InputSetJson { get; set; } = "{}";

    /// <summary>
    /// JSON result from LLM analysis. Schema varies by AnalysisType:
    /// <list type="bullet">
    ///   <item><description>GenerateInsights: { overview: string, emergingThemes: string[], crossIndustrySignals: string[], hypotheses: { hypothesis, rationale, supportingEvidence }[], suggestedExperiments: { experiment, objective, expectedSignal }[], risksAndUnknowns: string[], confidence: number }</description></item>
    ///   <item><description>ComparePapers: { similarities: string[], differences: string[], combinedStrengths: string[] }</description></item>
    ///   <item><description>CompareFields: { fieldComparisons: { field1, field2, similarities, differences }[] }</description></item>
    /// </list>
    /// </summary>
    public string? ResultJson { get; set; }

    public string? CreatedBy { get; set; }

    [ForeignKey(nameof(JobId))]
    public Job? Job { get; set; }
}

