using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Application.Summaries;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.Tests;

public sealed class PaperAndSummaryEmbeddingIntegrationTests
{
    [Fact]
    public async Task CreateAsync_indexes_new_paper_abstract()
    {
        await using var dbContext = CreateDbContext();
        var indexingService = new RecordingEmbeddingIndexingService();
        var service = CreatePaperService(dbContext, indexingService);

        var created = await service.CreateAsync(
            new CreatePaperCommand(
                null,
                "10.1000/test",
                "Local OCR paper",
                "This abstract should be embedded.",
                ["Ada Lovelace"],
                2026,
                "TestConf",
                7,
                PaperSource.Manual,
                PaperStatus.Draft,
                new JsonObject()),
            CancellationToken.None);

        Assert.Single(indexingService.PaperIds);
        Assert.Contains(created.Id, indexingService.PaperIds);
    }

    [Fact]
    public async Task UpdateAsync_reindexes_paper_after_abstract_change()
    {
        await using var dbContext = CreateDbContext();
        var indexingService = new RecordingEmbeddingIndexingService();
        var paper = new Paper
        {
            Title = "Original",
            Abstract = "Old abstract",
            Authors = ["Grace Hopper"],
            Source = PaperSource.Manual,
            Status = PaperStatus.Draft
        };

        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var service = CreatePaperService(dbContext, indexingService);

        await service.UpdateAsync(
            paper.Id,
            new UpdatePaperCommand(
                null,
                null,
                "New abstract for reindexing",
                null,
                null,
                null,
                null,
                null,
                null),
            CancellationToken.None);

        Assert.Single(indexingService.PaperIds);
        Assert.Contains(paper.Id, indexingService.PaperIds);
    }

    [Fact]
    public async Task CreateAsync_indexes_summary_search_text()
    {
        await using var dbContext = CreateDbContext();
        var indexingService = new RecordingEmbeddingIndexingService();
        var paper = new Paper
        {
            Title = "Summary target",
            Authors = ["Margaret Hamilton"],
            Source = PaperSource.Manual,
            Status = PaperStatus.Ready
        };

        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var service = CreateSummaryService(dbContext, indexingService);

        var created = await service.CreateAsync(
            new CreateSummaryCommand(
                paper.Id,
                "openrouter/free",
                "v1",
                SummaryStatus.Generated,
                new JsonObject { ["shortSummary"] = "Useful summary" },
                "summary text to embed"),
            CancellationToken.None);

        Assert.Single(indexingService.SummaryIds);
        Assert.Contains(created.Id, indexingService.SummaryIds);
    }

    [Fact]
    public async Task UpdateAsync_reindexes_summary_when_search_text_changes()
    {
        await using var dbContext = CreateDbContext();
        var indexingService = new RecordingEmbeddingIndexingService();
        var paper = new Paper
        {
            Title = "Summary target",
            Authors = ["Katherine Johnson"],
            Source = PaperSource.Manual,
            Status = PaperStatus.Ready
        };
        var summary = new PaperSummary
        {
            Paper = paper,
            ModelName = "openrouter/free",
            PromptVersion = "v1",
            Status = SummaryStatus.Generated,
            SearchText = "old search text"
        };

        dbContext.Papers.Add(paper);
        dbContext.PaperSummaries.Add(summary);
        await dbContext.SaveChangesAsync();

        var service = CreateSummaryService(dbContext, indexingService);

        await service.UpdateAsync(
            summary.Id,
            new UpdateSummaryCommand(
                SummaryStatus.Approved,
                new JsonObject { ["shortSummary"] = "Updated" },
                "new search text"),
            CancellationToken.None);

        Assert.Single(indexingService.SummaryIds);
        Assert.Contains(summary.Id, indexingService.SummaryIds);
    }

    private static PaperService CreatePaperService(
        ApplicationDbContext dbContext,
        IEmbeddingIndexingService indexingService)
    {
        var jobService = new JobService(dbContext, NullLogger<JobService>.Instance);
        return new PaperService(
            dbContext,
            new FakeSemanticScholarClient(Array.Empty<SemanticScholarPaperImportModel>()),
            jobService,
            indexingService,
            NullLogger<PaperService>.Instance);
    }

    private static SummaryService CreateSummaryService(
        ApplicationDbContext dbContext,
        IEmbeddingIndexingService indexingService) =>
        new(dbContext, indexingService, NullLogger<SummaryService>.Instance);

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class RecordingEmbeddingIndexingService : IEmbeddingIndexingService
    {
        public List<Guid> PaperIds { get; } = [];
        public List<Guid> SummaryIds { get; } = [];

        public Task UpsertPaperAbstractAsync(Paper paper, CancellationToken cancellationToken)
        {
            PaperIds.Add(paper.Id);
            return Task.CompletedTask;
        }

        public Task UpsertPaperAbstractAsync(IEnumerable<Paper> papers, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertSummaryAsync(PaperSummary summary, CancellationToken cancellationToken)
        {
            SummaryIds.Add(summary.Id);
            return Task.CompletedTask;
        }

        public Task UpsertDocumentChunkAsync(DocumentChunk chunk, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpsertDocumentChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSemanticScholarClient(IReadOnlyCollection<SemanticScholarPaperImportModel> results) : ISemanticScholarClient
    {
        public Task<IReadOnlyCollection<SemanticScholarPaperImportModel>> SearchPapersAsync(
            IReadOnlyCollection<string> queries,
            int limit,
            CancellationToken cancellationToken) => Task.FromResult(results);

        public Task<SemanticScholarPaperDetails?> GetPaperDetailsAsync(string arXivId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<SemanticScholarPaperDetails?>(null);
        }
    }
}
