using AutonomousResearchAgent.Api.Contracts.Common;
using AutonomousResearchAgent.Api.Contracts.Documents;
using AutonomousResearchAgent.Api.Contracts.Jobs;
using AutonomousResearchAgent.Api.Contracts.Papers;
using AutonomousResearchAgent.Api.Contracts.Search;
using AutonomousResearchAgent.Api.Contracts.Summaries;
using AutonomousResearchAgent.Api.Controllers;
using AutonomousResearchAgent.Application.Citations;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Documents;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Infrastructure.Tests;

public sealed class ControllerTests
{
    private static void SetupControllerContext(ControllerBase controller)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new("sub", Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    private static void SetupControllerContextWithUserId(ControllerBase controller, Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task PapersController_GetPapers_returns_ok_with_paged_response()
    {
        var mockService = new Mock<IPaperService>();
        var mockCitationGraphService = new Mock<ICitationGraphService>();
        var controller = new PapersController(mockService.Object, mockCitationGraphService.Object);
        SetupControllerContext(controller);
        var cancellationToken = CancellationToken.None;

        var pagedResult = new PagedResult<PaperListItem>(
            new List<PaperListItem>
            {
                new(Guid.NewGuid(), "Paper 1", new[] { "Author 1" }, 2025, "Venue A", 10, PaperSource.Manual, PaperStatus.Draft, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
                new(Guid.NewGuid(), "Paper 2", new[] { "Author 2" }, 2024, "Venue B", 5, PaperSource.SemanticScholar, PaperStatus.Ready, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            },
            1,
            25,
            2);

        mockService.Setup(s => s.ListAsync(It.IsAny<PaperQuery>(), It.IsAny<Guid?>(), cancellationToken))
            .ReturnsAsync(pagedResult);

        var result = await controller.GetPapers(new PaperQueryRequest(), cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<PaperListItemDto>>(okResult.Value);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(1, response.PageNumber);
        Assert.Equal(25, response.PageSize);
        Assert.Equal(2, response.TotalCount);
    }

    [Fact]
    public async Task PapersController_GetPaper_returns_ok_with_paper_detail()
    {
        var mockService = new Mock<IPaperService>();
        var mockCitationGraphService = new Mock<ICitationGraphService>();
        var controller = new PapersController(mockService.Object, mockCitationGraphService.Object);
        SetupControllerContext(controller);
        var paperId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var paperDetail = new PaperDetail(
            paperId, "ss-1", "10.1000/test", "Test Paper", "Abstract", new[] { "Author" }, 2025, "Venue", 10,
            PaperSource.Manual, PaperStatus.Draft, null, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.GetByIdAsync(paperId, It.IsAny<Guid?>(), cancellationToken))
            .ReturnsAsync(paperDetail);

        var result = await controller.GetPaper(paperId, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PaperDetailDto>(okResult.Value);
        Assert.Equal(paperId, response.Id);
        Assert.Equal("Test Paper", response.Title);
    }

    [Fact]
    public async Task PapersController_CreatePaper_returns_created_with_paper_detail()
    {
        var mockService = new Mock<IPaperService>();
        var mockCitationGraphService = new Mock<ICitationGraphService>();
        var controller = new PapersController(mockService.Object, mockCitationGraphService.Object);
        SetupControllerContext(controller);
        var cancellationToken = CancellationToken.None;

        var request = new CreatePaperRequest
        {
            Title = "New Paper",
            Abstract = "Abstract",
            Authors = new List<string> { "Author" },
            Year = 2025
        };

        var created = new PaperDetail(
            Guid.NewGuid(), null, null, "New Paper", "Abstract", new[] { "Author" }, 2025, null, 0,
            PaperSource.Manual, PaperStatus.Draft, null, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.CreateAsync(It.IsAny<CreatePaperCommand>(), cancellationToken))
            .ReturnsAsync(created);

        var result = await controller.CreatePaper(request, cancellationToken);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PapersController.GetPaper), createdResult.ActionName);
        var response = Assert.IsType<PaperDetailDto>(createdResult.Value);
        Assert.Equal("New Paper", response.Title);
    }

    [Fact]
    public async Task PapersController_UpdatePaper_returns_ok_with_updated_paper()
    {
        var mockService = new Mock<IPaperService>();
        var mockCitationGraphService = new Mock<ICitationGraphService>();
        var controller = new PapersController(mockService.Object, mockCitationGraphService.Object);
        SetupControllerContext(controller);
        var paperId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var request = new UpdatePaperRequest { Title = "Updated Title" };

        var updated = new PaperDetail(
            paperId, null, null, "Updated Title", null, Array.Empty<string>(), 0, null, 0,
            PaperSource.Manual, PaperStatus.Draft, null, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.UpdateAsync(paperId, It.IsAny<UpdatePaperCommand>(), cancellationToken))
            .ReturnsAsync(updated);

        var result = await controller.UpdatePaper(paperId, request, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PaperDetailDto>(okResult.Value);
        Assert.Equal("Updated Title", response.Title);
    }

    [Fact]
    public async Task JobsController_GetJobs_returns_ok_with_paged_response()
    {
        var mockService = new Mock<IJobService>();
        var controller = new JobsController(mockService.Object);
        SetupControllerContext(controller);
        var cancellationToken = CancellationToken.None;

        var pagedResult = new PagedResult<JobModel>(
            new List<JobModel>
            {
                new(Guid.NewGuid(), JobType.ImportPapers, JobStatus.Queued, null, null, null, null, "user1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            },
            1,
            25,
            1);

        mockService.Setup(s => s.ListAsync(It.IsAny<JobQuery>(), cancellationToken))
            .ReturnsAsync(pagedResult);

        var result = await controller.GetJobs(new JobQueryRequest(), cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<JobDto>>(okResult.Value);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task JobsController_GetJob_returns_ok_with_job()
    {
        var mockService = new Mock<IJobService>();
        var controller = new JobsController(mockService.Object);
        SetupControllerContext(controller);
        var jobId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var job = new JobModel(jobId, JobType.ImportPapers, JobStatus.Completed, null, null, null, null, "user1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.GetByIdAsync(jobId, cancellationToken))
            .ReturnsAsync(job);

        var result = await controller.GetJob(jobId, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<JobDto>(okResult.Value);
        Assert.Equal(jobId, response.Id);
    }

    [Fact]
    public async Task JobsController_CreateImportJob_returns_created()
    {
        var mockService = new Mock<IJobService>();
        var controller = new JobsController(mockService.Object);
        SetupControllerContext(controller);
        var cancellationToken = CancellationToken.None;

        var request = new CreateImportJobRequest
        {
            Queries = new List<string> { "quantum computing" },
            Limit = 10,
            StoreImportedPapers = true
        };

        var createdJob = new JobModel(
            Guid.NewGuid(), JobType.ImportPapers, JobStatus.Queued, null, null, null, null, "test-user", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.CreateAsync(It.IsAny<CreateJobCommand>(), cancellationToken))
            .ReturnsAsync(createdJob);

        var result = await controller.CreateImportJob(request, cancellationToken);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(JobsController.GetJob), createdResult.ActionName);
    }

    [Fact]
    public async Task JobsController_CreateSummarizeJob_returns_created()
    {
        var mockService = new Mock<IJobService>();
        var controller = new JobsController(mockService.Object);
        SetupControllerContext(controller);
        var cancellationToken = CancellationToken.None;

        var paperId = Guid.NewGuid();
        var request = new CreateSummarizeJobRequest
        {
            PaperId = paperId,
            ModelName = "test-model",
            PromptVersion = "v1"
        };

        var createdJob = new JobModel(
            Guid.NewGuid(), JobType.SummarizePaper, JobStatus.Queued, null, paperId, null, null, "test-user", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.CreateAsync(It.IsAny<CreateJobCommand>(), cancellationToken))
            .ReturnsAsync(createdJob);

        var result = await controller.CreateSummarizeJob(request, cancellationToken);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(JobsController.GetJob), createdResult.ActionName);
    }

    [Fact]
    public async Task JobsController_RetryJob_returns_ok()
    {
        var mockService = new Mock<IJobService>();
        var controller = new JobsController(mockService.Object);
        SetupControllerContext(controller);
        var jobId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var request = new RetryJobRequest { Reason = "Retry reason" };
        var retriedJob = new JobModel(jobId, JobType.ImportPapers, JobStatus.Queued, null, null, null, null, "test-user", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.RetryAsync(jobId, It.IsAny<RetryJobCommand>(), cancellationToken))
            .ReturnsAsync(retriedJob);

        var result = await controller.RetryJob(jobId, request, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<JobDto>(okResult.Value);
    }

    [Fact]
    public async Task SearchController_Search_returns_ok_with_paged_results()
    {
        var mockService = new Mock<ISearchService>();
        var controller = new SearchController(mockService.Object);
        var cancellationToken = CancellationToken.None;

        var pagedResult = new PagedResult<SearchResultModel>(
            new List<SearchResultModel>
            {
                new(Guid.NewGuid(), "Paper 1", "Abstract", new[] { "Author" }, 2025, "Venue", 0.95, "keyword", null)
            },
            1,
            10,
            1);

        mockService.Setup(s => s.SearchAsync(It.IsAny<SearchRequestModel>(), cancellationToken))
            .ReturnsAsync(pagedResult);

        var result = await controller.Search(new SearchRequest(), cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PagedResponse<SearchResultDto>>(okResult.Value);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task SearchController_SemanticSearch_returns_ok_with_paged_results()
    {
        var mockService = new Mock<ISearchService>();
        var controller = new SearchController(mockService.Object);
        var cancellationToken = CancellationToken.None;

        var pagedResult = new PagedResult<SearchResultModel>(
            new List<SearchResultModel>(),
            1,
            10,
            0);

        mockService.Setup(s => s.SemanticSearchAsync(It.IsAny<SemanticSearchRequestModel>(), cancellationToken))
            .ReturnsAsync(pagedResult);

        var result = await controller.SemanticSearch(new SemanticSearchRequest { Query = "test" }, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<PagedResponse<SearchResultDto>>(okResult.Value);
    }

    [Fact]
    public async Task SearchController_HybridSearch_returns_ok_with_paged_results()
    {
        var mockService = new Mock<ISearchService>();
        var controller = new SearchController(mockService.Object);
        var cancellationToken = CancellationToken.None;

        var pagedResult = new PagedResult<SearchResultModel>(
            new List<SearchResultModel>(),
            1,
            10,
            0);

        mockService.Setup(s => s.HybridSearchAsync(It.IsAny<HybridSearchRequestModel>(), cancellationToken))
            .ReturnsAsync(pagedResult);

        var result = await controller.HybridSearch(new HybridSearchRequest { Query = "test" }, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<PagedResponse<SearchResultDto>>(okResult.Value);
    }

    [Fact]
    public async Task SummariesController_GetSummariesForPaper_returns_ok_with_summaries_list()
    {
        var mockService = new Mock<ISummaryService>();
        var controller = new SummariesController(mockService.Object, Mock.Of<ISummaryDiffService>(), Mock.Of<ISummarizationService>(), Mock.Of<IPromptVersionService>());
        SetupControllerContext(controller);
        var paperId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var summaries = new List<SummaryModel>
        {
            new(Guid.NewGuid(), paperId, "model", "v1", SummaryStatus.Generated, "Summary text", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        mockService.Setup(s => s.ListForPaperAsync(paperId, cancellationToken))
            .ReturnsAsync(summaries);

        var result = await controller.GetSummariesForPaper(paperId, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<List<SummaryDto>>(okResult.Value);
        Assert.Single(response);
    }

    [Fact]
    public async Task SummariesController_CreateSummary_returns_created()
    {
        var mockService = new Mock<ISummaryService>();
        var controller = new SummariesController(mockService.Object, Mock.Of<ISummaryDiffService>(), Mock.Of<ISummarizationService>(), Mock.Of<IPromptVersionService>());
        SetupControllerContext(controller);
        var paperId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var request = new CreateSummaryRequest
        {
            ModelName = "test-model",
            PromptVersion = "v1",
            Summary = "Test summary"
        };

        var created = new SummaryModel(
            Guid.NewGuid(), paperId, "test-model", "v1", SummaryStatus.Generated, "Test summary", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.CreateAsync(It.IsAny<CreateSummaryCommand>(), cancellationToken))
            .ReturnsAsync(created);

        var result = await controller.CreateSummary(paperId, request, cancellationToken);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(SummariesController.GetSummary), createdResult.ActionName);
    }

    [Fact]
    public async Task SummariesController_GetSummary_returns_ok()
    {
        var mockService = new Mock<ISummaryService>();
        var controller = new SummariesController(mockService.Object, Mock.Of<ISummaryDiffService>(), Mock.Of<ISummarizationService>(), Mock.Of<IPromptVersionService>());
        SetupControllerContext(controller);
        var summaryId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var summary = new SummaryModel(
            summaryId, Guid.NewGuid(), "model", "v1", SummaryStatus.Generated, "Summary", null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.GetByIdAsync(summaryId, cancellationToken))
            .ReturnsAsync(summary);

        var result = await controller.GetSummary(summaryId, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SummaryDto>(okResult.Value);
        Assert.Equal(summaryId, response.Id);
    }

    [Fact]
    public async Task SummariesController_UpdateSummary_returns_ok()
    {
        var mockService = new Mock<ISummaryService>();
        var controller = new SummariesController(mockService.Object, Mock.Of<ISummaryDiffService>(), Mock.Of<ISummarizationService>(), Mock.Of<IPromptVersionService>());
        SetupControllerContext(controller);
        var summaryId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var request = new UpdateSummaryRequest { Summary = JsonNode.Parse("\"Updated summary\"") };

        var updated = new SummaryModel(
            summaryId, Guid.NewGuid(), "model", "v1", SummaryStatus.Generated, JsonNode.Parse("\"Updated summary\""), null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.UpdateAsync(summaryId, It.IsAny<UpdateSummaryCommand>(), cancellationToken))
            .ReturnsAsync(updated);

        var result = await controller.UpdateSummary(summaryId, request, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SummaryDto>(okResult.Value);
        Assert.Equal("Updated summary", response.Summary?.AsValue().GetValue<string>());
    }

    [Fact]
    public async Task SummariesController_ApproveSummary_returns_ok()
    {
        var mockService = new Mock<ISummaryService>();
        var controller = new SummariesController(mockService.Object, Mock.Of<ISummaryDiffService>(), Mock.Of<ISummarizationService>(), Mock.Of<IPromptVersionService>());
        SetupControllerContext(controller);
        var summaryId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var request = new ReviewSummaryRequest { Notes = "Approved" };

        var reviewed = new SummaryModel(
            summaryId, Guid.NewGuid(), "model", "v1", SummaryStatus.Approved, "Summary", "reviewer", DateTimeOffset.UtcNow, "Approved", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.ReviewAsync(summaryId, It.IsAny<ReviewSummaryCommand>(), cancellationToken))
            .ReturnsAsync(reviewed);

        var result = await controller.ApproveSummary(summaryId, request, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SummaryDto>(okResult.Value);
        Assert.Equal("Approved", response.Status);
    }

    [Fact]
    public async Task SummariesController_RejectSummary_returns_ok()
    {
        var mockService = new Mock<ISummaryService>();
        var controller = new SummariesController(mockService.Object, Mock.Of<ISummaryDiffService>(), Mock.Of<ISummarizationService>(), Mock.Of<IPromptVersionService>());
        SetupControllerContext(controller);
        var summaryId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var request = new ReviewSummaryRequest { Notes = "Rejected" };

        var reviewed = new SummaryModel(
            summaryId, Guid.NewGuid(), "model", "v1", SummaryStatus.Rejected, "Summary", "reviewer", DateTimeOffset.UtcNow, "Rejected", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.ReviewAsync(summaryId, It.IsAny<ReviewSummaryCommand>(), cancellationToken))
            .ReturnsAsync(reviewed);

        var result = await controller.RejectSummary(summaryId, request, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SummaryDto>(okResult.Value);
        Assert.Equal("Rejected", response.Status);
    }

    [Fact]
    public async Task PaperDocumentsController_GetDocuments_returns_ok_with_documents_list()
    {
        var mockService = new Mock<IPaperDocumentService>();
        var controller = new PaperDocumentsController(mockService.Object);
        SetupControllerContext(controller);
        var paperId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var documents = new List<PaperDocumentModel>
        {
            new(Guid.NewGuid(), paperId, "url", "file.pdf", "application/pdf", "path", PaperDocumentStatus.Extracted, false, "text", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        mockService.Setup(s => s.ListByPaperIdAsync(paperId, cancellationToken))
            .ReturnsAsync(documents);

        var result = await controller.GetDocuments(paperId, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<List<PaperDocumentDto>>(okResult.Value);
        Assert.Single(response);
    }

    [Fact]
    public async Task PaperDocumentsController_GetDocument_returns_ok()
    {
        var mockService = new Mock<IPaperDocumentService>();
        var controller = new PaperDocumentsController(mockService.Object);
        SetupControllerContext(controller);
        var paperId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var document = new PaperDocumentModel(
            documentId, paperId, "url", "file.pdf", "application/pdf", "path", PaperDocumentStatus.Extracted, false, "text", null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.GetByIdAsync(paperId, documentId, cancellationToken))
            .ReturnsAsync(document);

        var result = await controller.GetDocument(paperId, documentId, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PaperDocumentDto>(okResult.Value);
        Assert.Equal(documentId, response.Id);
    }

    [Fact]
    public async Task PaperDocumentsController_CreateDocument_returns_created()
    {
        var mockService = new Mock<IPaperDocumentService>();
        var controller = new PaperDocumentsController(mockService.Object);
        SetupControllerContext(controller);
        var paperId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var request = new CreatePaperDocumentRequest
        {
            SourceUrl = "https://example.org/doc.pdf",
            FileName = "doc.pdf",
            MediaType = "application/pdf"
        };

        var created = new PaperDocumentModel(
            Guid.NewGuid(), paperId, "https://example.org/doc.pdf", "doc.pdf", "application/pdf", null, PaperDocumentStatus.Pending, false, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.CreateAsync(It.IsAny<CreatePaperDocumentCommand>(), cancellationToken))
            .ReturnsAsync(created);

        var result = await controller.CreateDocument(paperId, request, cancellationToken);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(PaperDocumentsController.GetDocument), createdResult.ActionName);
    }

    [Fact]
    public async Task PaperDocumentsController_QueueProcessing_returns_ok()
    {
        var mockService = new Mock<IPaperDocumentService>();
        var controller = new PaperDocumentsController(mockService.Object);
        SetupControllerContext(controller);
        var paperId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var cancellationToken = CancellationToken.None;

        var request = new QueuePaperDocumentProcessingRequest();

        var updated = new PaperDocumentModel(
            documentId, paperId, "url", "file.pdf", "application/pdf", "path", PaperDocumentStatus.Queued, false, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        mockService.Setup(s => s.QueueProcessingAsync(paperId, documentId, It.IsAny<QueuePaperDocumentProcessingCommand>(), cancellationToken))
            .ReturnsAsync(updated);

        var result = await controller.QueueProcessing(paperId, documentId, request, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PaperDocumentDto>(okResult.Value);
        Assert.Equal("Queued", response.Status);
    }
}
