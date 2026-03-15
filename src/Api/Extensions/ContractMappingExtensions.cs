using AutonomousResearchAgent.Api.Contracts.Analysis;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Documents;
using AutonomousResearchAgent.Api.Contracts.Jobs;
using AutonomousResearchAgent.Api.Contracts.Papers;
using AutonomousResearchAgent.Api.Contracts.Search;
using AutonomousResearchAgent.Api.Contracts.Summaries;
using AutonomousResearchAgent.Application.Analysis;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Documents;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Api.Extensions;

public static class ContractMappingExtensions
{
    public static PagedResponse<TDestination> ToPagedResponse<TSource, TDestination>(
        this PagedResult<TSource> result,
        Func<TSource, TDestination> map) =>
        new(result.Items.Select(map).ToList(), result.PageNumber, result.PageSize, result.TotalCount);

    public static PaperQuery ToApplicationModel(this PaperQueryRequest request) =>
        new(
            request.PageNumber,
            request.PageSize,
            request.Query,
            request.Year,
            request.Venue,
            ParseNullableEnum<PaperSource>(request.Source),
            ParseNullableEnum<PaperStatus>(request.Status),
            request.SortBy,
            ParseSortDirection(request.SortDirection));

    public static CreatePaperCommand ToApplicationModel(this CreatePaperRequest request) =>
        new(
            request.SemanticScholarId,
            request.Doi,
            request.Title,
            request.Abstract,
            request.Authors,
            request.Year,
            request.Venue,
            request.CitationCount,
            ParseEnum(request.Source, PaperSource.Manual),
            ParseEnum(request.Status, PaperStatus.Draft),
            request.Metadata);

    public static UpdatePaperCommand ToApplicationModel(this UpdatePaperRequest request) =>
        new(
            request.Doi,
            request.Title,
            request.Abstract,
            request.Authors,
            request.Year,
            request.Venue,
            request.CitationCount,
            ParseNullableEnum<PaperStatus>(request.Status),
            request.Metadata);

    public static ImportPapersCommand ToApplicationModel(this ImportPapersRequest request) =>
        new(request.Queries, request.Limit, request.StoreImportedPapers);

    public static CreatePaperDocumentCommand ToApplicationModel(this CreatePaperDocumentRequest request, Guid paperId) =>
        new(paperId, request.SourceUrl, request.FileName, request.MediaType, request.RequiresOcr, request.Metadata);

    public static QueuePaperDocumentProcessingCommand ToApplicationModel(this QueuePaperDocumentProcessingRequest request, string? requestedBy) =>
        new(requestedBy, request.Force);

    public static CreateSummaryCommand ToApplicationModel(this CreateSummaryRequest request, Guid paperId) =>
        new(
            paperId,
            request.ModelName,
            request.PromptVersion,
            ParseEnum(request.Status, SummaryStatus.Generated),
            request.Summary,
            request.SearchText);

    public static UpdateSummaryCommand ToApplicationModel(this UpdateSummaryRequest request) =>
        new(ParseNullableEnum<SummaryStatus>(request.Status), request.Summary, request.SearchText);

    public static ReviewSummaryCommand ToApprovedReviewCommand(this ReviewSummaryRequest request, string? reviewer) =>
        new(SummaryStatus.Approved, reviewer, request.Notes);

    public static ReviewSummaryCommand ToRejectedReviewCommand(this ReviewSummaryRequest request, string? reviewer) =>
        new(SummaryStatus.Rejected, reviewer, request.Notes);

    public static SearchRequestModel ToApplicationModel(this SearchRequest request) =>
        new(request.Query, request.PageNumber, request.PageSize);

    public static SemanticSearchRequestModel ToApplicationModel(this SemanticSearchRequest request) =>
        new(request.Query, request.PageNumber, request.PageSize, request.MaxCandidates);

    public static HybridSearchRequestModel ToApplicationModel(this HybridSearchRequest request) =>
        new(request.Query, request.KeywordWeight, request.SemanticWeight, request.PageNumber, request.PageSize, request.MaxCandidates);

    public static JobQuery ToApplicationModel(this JobQueryRequest request) =>
        new(request.PageNumber, request.PageSize, ParseNullableEnum<JobType>(request.Type), ParseNullableEnum<JobStatus>(request.Status));

    public static CreateJobCommand ToApplicationModel(this CreateJobRequest request, string? createdBy) =>
        new(ParseEnum(request.Type, JobType.ImportPapers), request.Payload, request.TargetEntityId, createdBy);

    public static ComparePapersCommand ToApplicationModel(this ComparePapersRequest request, string? requestedBy) =>
        new(request.LeftPaperId, request.RightPaperId, requestedBy);

    public static CompareFieldsCommand ToApplicationModel(this CompareFieldsRequest request, string? requestedBy) =>
        new(request.LeftFilter, request.RightFilter, requestedBy);

    public static GenerateInsightsCommand ToApplicationModel(this GenerateInsightsRequest request, string? requestedBy) =>
        new(request.Filter, requestedBy);

    public static PaperListItemDto ToDto(this PaperListItem model) =>
        new(model.Id, model.Title, model.Authors, model.Year, model.Venue, model.CitationCount, model.Source.ToString(), model.Status.ToString(), model.CreatedAt, model.UpdatedAt);

    public static PaperDetailDto ToDto(this PaperDetail model) =>
        new(model.Id, model.SemanticScholarId, model.Doi, model.Title, model.Abstract, model.Authors, model.Year, model.Venue, model.CitationCount, model.Source.ToString(), model.Status.ToString(), model.Metadata, model.CreatedAt, model.UpdatedAt);

    public static ImportPapersResponse ToDto(this ImportPapersResult result) =>
        new(result.Papers.Select(p => p.ToDto()).ToList(), result.ImportedCount);

    public static PaperDocumentDto ToDto(this PaperDocumentModel model) =>
        new(model.Id, model.PaperId, model.SourceUrl, model.FileName, model.MediaType, model.StoragePath, model.Status.ToString(), model.RequiresOcr, model.ExtractedText, model.Metadata, model.LastError, model.DownloadedAt, model.ExtractedAt, model.CreatedAt, model.UpdatedAt);

    public static SummaryDto ToDto(this SummaryModel model) =>
        new(model.Id, model.PaperId, model.ModelName, model.PromptVersion, model.Status.ToString(), model.Summary, model.ReviewedBy, model.ReviewedAt, model.ReviewNotes, model.CreatedAt, model.UpdatedAt);

    public static SearchResultDto ToDto(this SearchResultModel model) =>
        new(model.PaperId, model.Title, model.Abstract, model.Authors, model.Year, model.Venue, model.Score, model.MatchType, model.Highlights);

    public static JobDto ToDto(this JobModel model) =>
        new(model.Id, model.Type.ToString(), model.Status.ToString(), model.Payload, model.Result, model.ErrorMessage, model.TargetEntityId, model.CreatedBy, model.CreatedAt, model.UpdatedAt);

    public static AnalysisResultDto ToDto(this AnalysisResultModel model) =>
        new(model.Id, model.JobId, model.AnalysisType.ToString(), model.InputSet, model.Result, model.CreatedBy, model.CreatedAt, model.UpdatedAt);

    public static AnalysisJobStatusDto ToDto(this AnalysisJobStatusModel model) =>
        new(model.JobId, model.Status.ToString(), model.ErrorMessage, model.Result?.ToDto());

    private static TEnum ParseEnum<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : defaultValue;

    private static TEnum? ParseNullableEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null
        : Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed
        : null;

    private static SortDirection ParseSortDirection(string? value) =>
        value?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? SortDirection.Asc : SortDirection.Desc;
}

