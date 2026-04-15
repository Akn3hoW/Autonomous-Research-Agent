using System.ComponentModel.DataAnnotations.Schema;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Domain.Entities;

/// <summary>
/// Represents a durable background job with type-specific payload and result data stored as JSONB.
/// </summary>
public sealed class Job : AuditableEntity
{
    public JobType Type { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Queued;

    /// <summary>
    /// JSON payload containing job-specific parameters. Schema varies by job type:
    /// <list type="bullet">
    ///   <item><description>ImportPapers: { queries: string[], limit: number, storeImportedPapers: boolean }</description></item>
    ///   <item><description>SummarizePaper: { paperId: guid, modelName?: string, promptVersion?: string, abTestSessionId?: guid }</description></item>
    ///   <item><description>ProcessPaperDocument: { documentId: guid }</description></item>
    ///   <item><description>ResearchGoal: { goal: string, maxPapers?: number, field?: string }</description></item>
    ///   <item><description>SearchPapers: { goal: string, maxPapers?: number, field?: string }</description></item>
    /// </list>
    /// </summary>
    public string PayloadJson { get; set; } = "{}";

    /// <summary>
    /// JSON result from job execution. Schema varies by job type:
    /// <list type="bullet">
    ///   <item><description>ImportPapers: { importedCount: number, paperIds: guid[] }</description></item>
    ///   <item><description>SummarizePaper: { summaryId: guid, paperId: guid, modelName: string }</description></item>
    ///   <item><description>ProcessPaperDocument: { documentId: guid, status: string, storagePath: string, extractedAt: datetime }</description></item>
    ///   <item><description>ResearchGoal: { goal: string, childJobs: { id, type, status, result }[] }</description></item>
    /// </list>
    /// </summary>
    public string? ResultJson { get; set; }

    public string? ErrorMessage { get; set; }
    public Guid? TargetEntityId { get; set; }
    public string? CreatedBy { get; set; }
    public Guid? ParentJobId { get; set; }

    [ForeignKey(nameof(ParentJobId))]
    public Job? ParentJob { get; set; }

    public ICollection<Job> ChildJobs { get; set; } = [];
}

