using AutonomousResearchAgent.Api.Contracts.Analysis;
using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Concepts;
using AutonomousResearchAgent.Api.Contracts.Documents;
using AutonomousResearchAgent.Api.Contracts.Jobs;
using AutonomousResearchAgent.Api.Contracts.Papers;
using AutonomousResearchAgent.Api.Contracts.Search;
using AutonomousResearchAgent.Api.Contracts.Summaries;
using AutonomousResearchAgent.Api.Contracts.Watchlist;
using AutonomousResearchAgent.Application.Analysis;
using AutonomousResearchAgent.Application.Citations;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Concepts;
using AutonomousResearchAgent.Application.Documents;
using AutonomousResearchAgent.Application.Duplicates;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Application.Watchlist;
using AutonomousResearchAgent.Domain.Enums;

namespace AutonomousResearchAgent.Api.Extensions;

public static class ContractMappingExtensions
{
    public static PagedResponse<TDestination> ToPagedResponse<TSource, TDestination>(
        this PagedResult<TSource> result,
        Func<TSource, TDestination> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);

        return new PagedResponse<TDestination>(result.Items.Select(map).ToList(), result.PageNumber, result.PageSize, result.TotalCount);
    }

    public static PaperQuery ToApplicationModel(this PaperQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new PaperQuery(
            request.PageNumber,
            request.PageSize,
            request.Query,
            request.Year,
            request.Venue,
            ParseNullableEnum<PaperSource>(request.Source),
            ParseNullableEnum<PaperStatus>(request.Status),
            request.Tag,
            request.SortBy,
            ParseSortDirection(request.SortDirection));
    }

    public static CreatePaperCommand ToApplicationModel(this CreatePaperRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreatePaperCommand(
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
    }

    public static UpdatePaperCommand ToApplicationModel(this UpdatePaperRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdatePaperCommand(
            request.Doi,
            request.Title,
            request.Abstract,
            request.Authors,
            request.Year,
            request.Venue,
            request.CitationCount,
            ParseNullableEnum<PaperStatus>(request.Status),
            request.Metadata);
    }

    public static ImportPapersCommand ToApplicationModel(this ImportPapersRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ImportPapersCommand(request.Queries, request.Limit, request.StoreImportedPapers, request.Source ?? "semanticscholar");
    }

    public static CreatePaperDocumentCommand ToApplicationModel(this CreatePaperDocumentRequest request, Guid paperId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreatePaperDocumentCommand(paperId, request.SourceUrl, request.FileName, request.MediaType, request.RequiresOcr, request.Metadata);
    }

    public static QueuePaperDocumentProcessingCommand ToApplicationModel(this QueuePaperDocumentProcessingRequest request, string? requestedBy)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new QueuePaperDocumentProcessingCommand(requestedBy, request.Force);
    }

    public static CreateSummaryCommand ToApplicationModel(this CreateSummaryRequest request, Guid paperId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateSummaryCommand(
            paperId,
            request.ModelName,
            request.PromptVersion,
            ParseEnum(request.Status, SummaryStatus.Generated),
            request.Summary,
            request.SearchText);
    }

    public static UpdateSummaryCommand ToApplicationModel(this UpdateSummaryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateSummaryCommand(ParseNullableEnum<SummaryStatus>(request.Status), request.Summary, request.SearchText);
    }

    public static ReviewSummaryCommand ToApprovedReviewCommand(this ReviewSummaryRequest request, string? reviewer)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ReviewSummaryCommand(SummaryStatus.Approved, reviewer, request.Notes);
    }

    public static ReviewSummaryCommand ToRejectedReviewCommand(this ReviewSummaryRequest request, string? reviewer)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ReviewSummaryCommand(SummaryStatus.Rejected, reviewer, request.Notes);
    }

    public static SearchRequestModel ToApplicationModel(this SearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SearchRequestModel(request.Query, request.PageNumber, request.PageSize);
    }

    public static SemanticSearchRequestModel ToApplicationModel(this SemanticSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SemanticSearchRequestModel(request.Query, request.PageNumber, request.PageSize, request.MaxCandidates);
    }

    public static HybridSearchRequestModel ToApplicationModel(this HybridSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new HybridSearchRequestModel(request.Query, request.KeywordWeight, request.SemanticWeight, request.PageNumber, request.PageSize, request.MaxCandidates);
    }

    public static JobQuery ToApplicationModel(this JobQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new JobQuery(request.PageNumber, request.PageSize, ParseNullableEnum<JobType>(request.Type), ParseNullableEnum<JobStatus>(request.Status));
    }

    public static CreateJobCommand ToApplicationModel(this CreateJobRequest request, string? createdBy)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateJobCommand(ParseEnum(request.Type, JobType.ImportPapers), request.Payload, request.TargetEntityId, createdBy);
    }

    public static ComparePapersCommand ToApplicationModel(this ComparePapersRequest request, string? requestedBy)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ComparePapersCommand(request.LeftPaperId, request.RightPaperId, requestedBy);
    }

    public static CompareFieldsCommand ToApplicationModel(this CompareFieldsRequest request, string? requestedBy)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CompareFieldsCommand(request.LeftFilter, request.RightFilter, requestedBy);
    }

    public static GenerateInsightsCommand ToApplicationModel(this GenerateInsightsRequest request, string? requestedBy)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new GenerateInsightsCommand(request.Filter, requestedBy);
    }

    public static PaperListItemDto ToDto(this PaperListItem model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new PaperListItemDto(model.Id, model.Title, model.Authors, model.Year, model.Venue, model.CitationCount, model.Source.ToString(), model.Status.ToString(), model.Tags, model.CreatedAt, model.UpdatedAt);
    }

    public static PaperDetailDto ToDto(this PaperDetail model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new PaperDetailDto(model.Id, model.SemanticScholarId, model.Doi, model.Title, model.Abstract, model.Authors, model.Year, model.Venue, model.CitationCount, model.Source.ToString(), model.Status.ToString(), model.Metadata, model.Tags, model.CreatedAt, model.UpdatedAt);
    }

    public static ImportPapersResponse ToDto(this ImportPapersResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ImportPapersResponse(result.Papers.Select(p => p.ToDto()).ToList(), result.ImportedCount);
    }

    public static CitationGraphResponse ToDto(this CitationGraphDto model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new CitationGraphResponse(
            model.Nodes.Select(n => new PaperNodeDto(n.Id, n.Title, n.Year, n.CitationCount, n.IsInDatabase)).ToList(),
            model.Edges.Select(e => new CitationEdgeDto(e.SourceId, e.TargetId, e.Context)).ToList());
    }

    public static PaperDocumentDto ToDto(this PaperDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new PaperDocumentDto(model.Id, model.PaperId, model.SourceUrl, model.FileName, model.MediaType, model.StoragePath, model.Status.ToString(), model.RequiresOcr, model.ExtractedText, model.Metadata, model.LastError, model.DownloadedAt, model.ExtractedAt, model.CreatedAt, model.UpdatedAt);
    }

    public static SummaryDto ToDto(this SummaryModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new SummaryDto(model.Id, model.PaperId, model.ModelName, model.PromptVersion, model.Status.ToString(), model.Summary, model.ReviewedBy, model.ReviewedAt, model.ReviewNotes, model.CreatedAt, model.UpdatedAt);
    }

    public static SummaryDiffDto ToDto(this SummaryDiffModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new SummaryDiffDto(
            model.PaperId,
            model.PaperTitle,
            new SummaryVersionDto(model.Summary1.Id, model.Summary1.ModelName, model.Summary1.PromptVersion, model.Summary1.CreatedAt, model.Summary1.SummaryText, model.Summary1.Status),
            new SummaryVersionDto(model.Summary2.Id, model.Summary2.ModelName, model.Summary2.PromptVersion, model.Summary2.CreatedAt, model.Summary2.SummaryText, model.Summary2.Status),
            new FieldDiffsDto(
                new FieldDiffDto(model.FieldDiffs.Summary.Left, model.FieldDiffs.Summary.Right, model.FieldDiffs.Summary.DiffHtml, model.FieldDiffs.Summary.Changed),
                new FieldDiffDto(model.FieldDiffs.ModelName.Left, model.FieldDiffs.ModelName.Right, model.FieldDiffs.ModelName.DiffHtml, model.FieldDiffs.ModelName.Changed),
                new FieldDiffDto(model.FieldDiffs.PromptVersion.Left, model.FieldDiffs.PromptVersion.Right, model.FieldDiffs.PromptVersion.DiffHtml, model.FieldDiffs.PromptVersion.Changed)),
            model.OverallSimilarity);
    }

    public static SearchResultDto ToDto(this SearchResultModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new SearchResultDto(model.PaperId, model.Title, model.Abstract, model.Authors, model.Year, model.Venue, model.Score, model.MatchType, model.Highlights);
    }

    public static ChunkSearchRequestModel ToApplicationModel(this ChunkSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ChunkSearchRequestModel(request.Query, request.PageNumber, request.PageSize, request.MaxCandidates);
    }

    public static ChunkSearchResultDto ToDto(this ChunkSearchResultModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ChunkSearchResultDto(model.ChunkId, model.PaperId, model.PaperTitle, model.ChunkText, model.Score);
    }

    public static JobDto ToDto(this JobModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new JobDto(
            model.Id,
            model.Type.ToString(),
            model.Status.ToString(),
            model.Payload,
            model.Result,
            model.ErrorMessage,
            model.TargetEntityId,
            model.CreatedBy,
            model.CreatedAt,
            model.UpdatedAt,
            model.ParentJobId,
            model.RetryCount,
            model.LastAttemptAt,
            model.RetryPolicy,
            model.DependsOnJobIds,
            model.WorkflowStep);
    }

    public static AnalysisResultDto ToDto(this AnalysisResultModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new AnalysisResultDto(model.Id, model.JobId, model.AnalysisType.ToString(), model.InputSet, model.Result, model.CreatedBy, model.CreatedAt, model.UpdatedAt);
    }

    public static AnalysisJobStatusDto ToDto(this AnalysisJobStatusModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new AnalysisJobStatusDto(model.JobId, model.Status.ToString(), model.ErrorMessage, model.Result?.ToDto());
    }

    public static SavedSearchQuery ToApplicationModel(this SavedSearchQueryRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new SavedSearchQuery(request.PageNumber, request.PageSize, userId, request.IsActive);
    }

    public static CreateSavedSearchCommand ToApplicationModel(this CreateSavedSearchRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreateSavedSearchCommand(userId, request.Query, request.Field, ParseEnum(request.Schedule, ScheduleType.Manual));
    }

    public static UpdateSavedSearchCommand ToApplicationModel(this UpdateSavedSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new UpdateSavedSearchCommand(request.Query, request.Field, ParseNullableEnum<ScheduleType>(request.Schedule), request.IsActive);
    }

    public static SavedSearchDto ToDto(this SavedSearchModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new SavedSearchDto(model.Id, model.UserId, model.Query, model.Field, model.Schedule.ToString(), model.LastRunAt, model.ResultCount, model.IsActive, model.CreatedAt, model.UpdatedAt);
    }

    public static NotificationQuery ToApplicationModel(this NotificationQueryRequest request, Guid userId)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new NotificationQuery(request.PageNumber, request.PageSize, userId, request.IsRead);
    }

    public static NotificationDto ToDto(this NotificationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new NotificationDto(model.Id, model.UserId, model.Title, model.Message, model.LinkUrl, model.IsRead, model.CreatedAt);
    }

    public static RunSavedSearchResponse ToDto(this RunSavedSearchResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new RunSavedSearchResponse(result.NewPapersCount, result.JobId);
    }

    public static DigestDto ToDto(this AutonomousResearchAgent.Application.Watchlist.DigestModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new DigestDto(model.Id, model.UserId, model.Frequency.ToString(), model.Topic, model.Content, model.NewPapersCount, model.CreatedAt);
    }

    public static ResearchGapReportDto ToDto(this ResearchGapReportModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ResearchGapReportDto(model.Id, model.Topic, model.GapAnalysis, model.CorpusCoverage, model.ExternalCoverage, model.SuggestedQueries, model.CreatedBy, model.CreatedAt);
    }

    public static DuplicatePairResponse ToDto(this DuplicatePairModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new DuplicatePairResponse(model.Id, model.PaperAId, model.PaperATitle, model.PaperBId, model.PaperBTitle, model.SimilarityScore, model.Status.ToString(), model.ReviewedByUserId, model.ReviewedAt?.UtcDateTime, model.Notes, model.CreatedAt);
    }

    public static DuplicatesResponse ToDto(this DuplicatesResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new DuplicatesResponse(result.Pairs.Select(p => p.ToDto()).ToList(), result.TotalCount, result.PendingCount);
    }

    public static ConceptDto ToDto(this ConceptModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ConceptDto(model.Id, model.PaperId, model.ConceptType.ToString(), model.Name, model.Confidence, model.CreatedAt);
    }

    public static ConceptStatisticsDto ToDto(this ConceptStatistics model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new ConceptStatisticsDto(model.ByType.Select(c => new ConceptTypeCountDto(c.ConceptType.ToString(), c.Count, c.PaperCount)).ToList(), model.TotalConcepts, model.TotalPapers);
    }

    public static AutonomousResearchAgent.Application.Summaries.CreateAbTestRequest ToApplicationModel(this AutonomousResearchAgent.Api.Contracts.Summaries.CreateAbTestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new AutonomousResearchAgent.Application.Summaries.CreateAbTestRequest(request.Name, request.PaperId, request.ModelNames);
    }

    public static AbTestSessionDto ToDto(this AbTestSessionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new AbTestSessionDto(model.Id, model.Name, model.PaperId, model.PaperTitle, model.Status, model.CreatedAt, model.CompletedAt,
            model.Results.Select(r => new SummaryResultDto(r.SummaryId, r.ModelName, r.Summary, r.SummaryStatus, r.CreatedAt, r.IsSelected)).ToArray());
    }

    public static PromptVersionDto ToDto(this PromptVersionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return new PromptVersionDto(model.Id, model.Name, model.Version, model.SystemPrompt, model.UserPromptTemplate, model.CreatedAt, model.IsActive);
    }

    public static CreatePromptVersionCommand ToApplicationModel(this CreatePromptVersionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new CreatePromptVersionCommand(request.Name, request.Version, request.SystemPrompt, request.UserPromptTemplate);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum defaultValue) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : defaultValue;

    private static TEnum? ParseNullableEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        string.IsNullOrWhiteSpace(value) ? null
        : Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed
        : null;

    private static SortDirection ParseSortDirection(string? value) =>
        value?.Equals("asc", StringComparison.OrdinalIgnoreCase) == true ? SortDirection.Asc : SortDirection.Desc;
}

