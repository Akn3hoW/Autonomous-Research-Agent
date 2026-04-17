using System.Text.Json;
using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Documents;
using AutonomousResearchAgent.Application.Duplicates;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Application.Trends;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.BackgroundJobs;
using AutonomousResearchAgent.Infrastructure.External.OpenRouter;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Infrastructure.Tests;

public sealed class AutonomousJobRunnerTests
{
    private readonly Mock<IPaperService> _paperServiceMock;
    private readonly Mock<ISummaryService> _summaryServiceMock;
    private readonly Mock<ISummarizationService> _summarizationServiceMock;
    private readonly Mock<IOpenRouterChatClient> _openRouterChatClientMock;
    private readonly IOptions<OpenRouterOptions> _openRouterOptions;
    private readonly ILoggerFactory _loggerFactory;

    public AutonomousJobRunnerTests()
    {
        _paperServiceMock = new Mock<IPaperService>();
        _summaryServiceMock = new Mock<ISummaryService>();
        _summarizationServiceMock = new Mock<ISummarizationService>();
        _openRouterChatClientMock = new Mock<IOpenRouterChatClient>();
        _openRouterOptions = Options.Create(new OpenRouterOptions { ApiKey = "test-key", Model = "test-model" });
        _loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private AutonomousJobRunner CreateRunner(ApplicationDbContext dbContext)
    {
        var paperDocumentProcessingService = new Mock<IPaperDocumentProcessingService>();

        paperDocumentProcessingService
            .Setup(s => s.ProcessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new PaperDocument
            {
                Id = id,
                PaperId = Guid.NewGuid(),
                Status = PaperDocumentStatus.Extracted,
                StoragePath = "/fake/path.pdf",
                ExtractedAt = DateTimeOffset.UtcNow
            });

        var mockJobService = new Mock<IJobService>();
        var mockTextChunkingService = new Mock<ITextChunkingService>();
        var mockEmbeddingIndexingService = new Mock<IEmbeddingIndexingService>();
        var mockDuplicateDetectionService = new Mock<IDuplicateDetectionService>();
        var mockSemanticScholarClient = new Mock<ISemanticScholarClient>();
        var mockTrendAnalysisService = new Mock<ITrendAnalysisService>();

        return new AutonomousJobRunner(
            dbContext,
            _paperServiceMock.Object,
            _summaryServiceMock.Object,
            _summarizationServiceMock.Object,
            paperDocumentProcessingService.Object,
            mockTextChunkingService.Object,
            mockEmbeddingIndexingService.Object,
            mockDuplicateDetectionService.Object,
            _openRouterChatClientMock.Object,
            mockSemanticScholarClient.Object,
            _openRouterOptions,
            Options.Create(new SummaryOptions { DefaultPromptVersion = "v1" }),
            _loggerFactory,
            mockTrendAnalysisService.Object);
    }

    [Fact]
    public async Task RunAsync_ImportPapers_job_calls_paper_service()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            PayloadJson = JsonSerializer.Serialize(new { queries = new[] { "machine learning" }, limit = 5, storeImportedPapers = true })
        };

        _paperServiceMock
            .Setup(s => s.ImportAsync(It.IsAny<ImportPapersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPapersResult(Array.Empty<PaperDetail>(), 0));

        await runner.RunAsync(job, CancellationToken.None);

        _paperServiceMock.Verify(
            s => s.ImportAsync(
                It.Is<ImportPapersCommand>(c => c.Queries.Contains("machine learning") && c.Limit == 5),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ImportPapers_sets_result_with_import_count()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            PayloadJson = JsonSerializer.Serialize(new { queries = new[] { "AI" }, limit = 10, storeImportedPapers = false })
        };
        var paperId = Guid.NewGuid();
        var papers = new List<PaperDetail>
        {
            new(paperId, null, null, "Test Paper", null, Array.Empty<string>(), null, null, 0, PaperSource.SemanticScholar, PaperStatus.Imported, null, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        };

        _paperServiceMock
            .Setup(s => s.ImportAsync(It.IsAny<ImportPapersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPapersResult(papers, 1));

        await runner.RunAsync(job, CancellationToken.None);

        var result = JsonNode.Parse(job.ResultJson!);
        Assert.Equal(1, result?["importedCount"]?.GetValue<int>());
        Assert.Contains(paperId, result?["paperIds"]?.AsArray().Select(n => n?.GetValue<Guid>()) ?? []);
    }

    [Fact]
    public async Task RunAsync_SummarizePaper_job_calls_services()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var paperId = Guid.NewGuid();
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.SummarizePaper,
            PayloadJson = JsonSerializer.Serialize(new { paperId = paperId, modelName = "test-model", promptVersion = "v1" })
        };
        var paper = new PaperDetail(paperId, null, null, "Test Paper", null, Array.Empty<string>(), null, null, 0, PaperSource.SemanticScholar, PaperStatus.Imported, null, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var summaryId = Guid.NewGuid();

        _paperServiceMock
            .Setup(s => s.GetByIdAsync(paperId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paper);

        _summarizationServiceMock
            .Setup(s => s.GenerateSummaryAsync(paper, "test-model", "v1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonNode.Parse("{\"shortSummary\":\"test\",\"longSummary\":\"test\",\"keyFindings\":[]}"));

        _summaryServiceMock
            .Setup(s => s.CreateAsync(It.IsAny<CreateSummaryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummaryModel(summaryId, paperId, "test-model", "v1", SummaryStatus.Generated, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        await runner.RunAsync(job, CancellationToken.None);

        _paperServiceMock.Verify(s => s.GetByIdAsync(paperId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
        _summarizationServiceMock.Verify(s => s.GenerateSummaryAsync(paper, "test-model", "v1", It.IsAny<CancellationToken>()), Times.Once);
        _summaryServiceMock.Verify(s => s.CreateAsync(It.IsAny<CreateSummaryCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_SummarizePaper_sets_result_with_summary_id()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var paperId = Guid.NewGuid();
        var summaryId = Guid.NewGuid();
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.SummarizePaper,
            PayloadJson = JsonSerializer.Serialize(new { paperId = paperId })
        };
        var paper = new PaperDetail(paperId, null, null, "Test Paper", null, Array.Empty<string>(), null, null, 0, PaperSource.SemanticScholar, PaperStatus.Imported, null, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        _paperServiceMock
            .Setup(s => s.GetByIdAsync(paperId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paper);

        _summarizationServiceMock
            .Setup(s => s.GenerateSummaryAsync(paper, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonNode.Parse("{\"shortSummary\":\"summary text\",\"longSummary\":\"long summary text\",\"keyFindings\":[]}"));

        _summaryServiceMock
            .Setup(s => s.CreateAsync(It.IsAny<CreateSummaryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummaryModel(summaryId, paperId, "default-model", "v1", SummaryStatus.Generated, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        await runner.RunAsync(job, CancellationToken.None);

        var result = JsonNode.Parse(job.ResultJson!);
        Assert.Equal(summaryId, result?["summaryId"]?.GetValue<Guid>());
        Assert.Equal(paperId, result?["paperId"]?.GetValue<Guid>());
    }

    [Fact]
    public async Task RunAsync_SummarizePaper_throws_when_paperId_missing()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.SummarizePaper,
            PayloadJson = JsonSerializer.Serialize(new { })
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(job, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_Analysis_job_calls_openrouter_client()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var paperId = Guid.NewGuid();
        var paper = new Paper
        {
            Id = paperId,
            Title = "Test Paper",
            Abstract = "Abstract",
            Authors = new List<string> { "Author 1" },
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };
        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.Analysis,
            PayloadJson = JsonSerializer.Serialize(new { filter = "" })
        };

        _openRouterChatClientMock
            .Setup(c => c.CreateJsonCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<double>()))
            .ReturnsAsync(JsonNode.Parse("{\"overview\":\"test analysis\"}"));

        await runner.RunAsync(job, CancellationToken.None);

        _openRouterChatClientMock.Verify(
            c => c.CreateJsonCompletionAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Test Paper")),
                It.IsAny<CancellationToken>(),
                It.IsAny<double>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_Analysis_sets_result_with_analysis_output()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var paper = new Paper
        {
            Id = Guid.NewGuid(),
            Title = "AI Paper",
            Abstract = "Abstract text",
            Authors = new List<string>(),
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };
        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.Analysis,
            PayloadJson = JsonSerializer.Serialize(new { filter = "" })
        };
        var analysisResult = JsonNode.Parse("{\"overview\":\"AI analysis\",\"confidence\":0.9}");

        _openRouterChatClientMock
            .Setup(c => c.CreateJsonCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<double>()))
            .ReturnsAsync(analysisResult);

        await runner.RunAsync(job, CancellationToken.None);

        var result = JsonNode.Parse(job.ResultJson!);
        Assert.Equal("AI analysis", result?["overview"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_ProcessPaperDocument_job_calls_processing_service()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var documentId = Guid.NewGuid();
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ProcessPaperDocument,
            PayloadJson = JsonSerializer.Serialize(new { documentId = documentId })
        };

        await runner.RunAsync(job, CancellationToken.None);

        Assert.NotNull(job.ResultJson);
        var result = JsonNode.Parse(job.ResultJson!);
        Assert.Equal(documentId, result?["documentId"]?.GetValue<Guid>());
        Assert.Equal("Extracted", result?["status"]?.GetValue<string>());
    }

    [Fact]
    public async Task RunAsync_ProcessPaperDocument_throws_when_documentId_missing()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ProcessPaperDocument,
            PayloadJson = JsonSerializer.Serialize(new { })
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(job, CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_Unsupported_job_type_throws()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = (JobType)999,
            PayloadJson = "{}"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(job, CancellationToken.None));
        Assert.Contains("Unsupported job type", ex.Message);
    }

    [Fact]
    public async Task RunAsync_ImportPapers_uses_default_limit_when_not_specified()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            PayloadJson = JsonSerializer.Serialize(new { queries = new[] { "test" } })
        };

        _paperServiceMock
            .Setup(s => s.ImportAsync(It.IsAny<ImportPapersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPapersResult(Array.Empty<PaperDetail>(), 0));

        await runner.RunAsync(job, CancellationToken.None);

        _paperServiceMock.Verify(
            s => s.ImportAsync(
                It.Is<ImportPapersCommand>(c => c.Limit == 10),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ImportPapers_uses_default_storeImportedPapers_when_not_specified()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            PayloadJson = JsonSerializer.Serialize(new { queries = new[] { "test" } })
        };

        _paperServiceMock
            .Setup(s => s.ImportAsync(It.IsAny<ImportPapersCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ImportPapersResult(Array.Empty<PaperDetail>(), 0));

        await runner.RunAsync(job, CancellationToken.None);

        _paperServiceMock.Verify(
            s => s.ImportAsync(
                It.Is<ImportPapersCommand>(c => c.StoreImportedPapers == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_SummarizePaper_uses_default_model_and_promptVersion_when_not_specified()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var paperId = Guid.NewGuid();
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.SummarizePaper,
            PayloadJson = JsonSerializer.Serialize(new { paperId = paperId })
        };
        var paper = new PaperDetail(paperId, null, null, "Test Paper", null, Array.Empty<string>(), null, null, 0, PaperSource.SemanticScholar, PaperStatus.Imported, null, Array.Empty<string>(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        _paperServiceMock
            .Setup(s => s.GetByIdAsync(paperId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paper);

        _summarizationServiceMock
            .Setup(s => s.GenerateSummaryAsync(paper, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonNode.Parse("{\"shortSummary\":\"test\",\"longSummary\":\"test\",\"keyFindings\":[]}"));

        _summaryServiceMock
            .Setup(s => s.CreateAsync(It.IsAny<CreateSummaryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummaryModel(Guid.NewGuid(), paperId, "test-model", "v1", SummaryStatus.Generated, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        await runner.RunAsync(job, CancellationToken.None);

        _summarizationServiceMock.Verify(
            s => s.GenerateSummaryAsync(paper, "test-model", "v1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_invalid_payload_json_throws()
    {
        await using var dbContext = CreateDbContext();
        var runner = CreateRunner(dbContext);
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Type = JobType.ImportPapers,
            PayloadJson = "not valid json {{{"
        };

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => runner.RunAsync(job, CancellationToken.None));
        Assert.Contains("not valid json", ex.Message);
    }
}
