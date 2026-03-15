using AutonomousResearchAgent.Application.Analysis;
using AutonomousResearchAgent.Application.Documents;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Entities;

namespace AutonomousResearchAgent.Infrastructure.Services;

internal static class ModelMappingExtensions
{
    public static PaperListItem ToListItem(this Paper paper) =>
        new(
            paper.Id,
            paper.Title,
            paper.Authors.AsReadOnly(),
            paper.Year,
            paper.Venue,
            paper.CitationCount,
            paper.Source,
            paper.Status,
            paper.CreatedAt,
            paper.UpdatedAt);

    public static PaperDetail ToDetail(this Paper paper) =>
        new(
            paper.Id,
            paper.SemanticScholarId,
            paper.Doi,
            paper.Title,
            paper.Abstract,
            paper.Authors.AsReadOnly(),
            paper.Year,
            paper.Venue,
            paper.CitationCount,
            paper.Source,
            paper.Status,
            JsonNodeMapper.Deserialize(paper.MetadataJson),
            paper.CreatedAt,
            paper.UpdatedAt);


    public static PaperDocumentModel ToModel(this PaperDocument document) =>
        new(
            document.Id,
            document.PaperId,
            document.SourceUrl,
            document.FileName,
            document.MediaType,
            document.StoragePath,
            document.Status,
            document.RequiresOcr,
            document.ExtractedText,
            JsonNodeMapper.Deserialize(document.MetadataJson),
            document.LastError,
            document.DownloadedAt,
            document.ExtractedAt,
            document.CreatedAt,
            document.UpdatedAt);

    public static SummaryModel ToModel(this PaperSummary summary) =>
        new(
            summary.Id,
            summary.PaperId,
            summary.ModelName,
            summary.PromptVersion,
            summary.Status,
            JsonNodeMapper.Deserialize(summary.SummaryJson),
            summary.ReviewedBy,
            summary.ReviewedAt,
            summary.ReviewNotes,
            summary.CreatedAt,
            summary.UpdatedAt);

    public static JobModel ToModel(this Job job) =>
        new(
            job.Id,
            job.Type,
            job.Status,
            JsonNodeMapper.Deserialize(job.PayloadJson),
            JsonNodeMapper.Deserialize(job.ResultJson),
            job.ErrorMessage,
            job.TargetEntityId,
            job.CreatedBy,
            job.CreatedAt,
            job.UpdatedAt);

    public static AnalysisResultModel ToModel(this AnalysisResult result) =>
        new(
            result.Id,
            result.JobId,
            result.AnalysisType,
            JsonNodeMapper.Deserialize(result.InputSetJson),
            JsonNodeMapper.Deserialize(result.ResultJson),
            result.CreatedBy,
            result.CreatedAt,
            result.UpdatedAt);
}

