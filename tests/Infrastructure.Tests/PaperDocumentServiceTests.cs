using System.Text.Json.Nodes;
using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Documents;
using AutonomousResearchAgent.Application.Jobs;
using AutonomousResearchAgent.Application.Papers;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Infrastructure.Tests;

public sealed class PaperDocumentServiceTests
{
    [Fact]
    public async Task CreateAsync_creates_pending_document_for_existing_paper()
    {
        await using var dbContext = CreateDbContext();
        var paper = new Paper { Title = "Test paper" };
        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var service = CreateDocumentService(dbContext);

        var created = await service.CreateAsync(
            new CreatePaperDocumentCommand(
                paper.Id,
                "https://example.org/paper.pdf",
                "paper.pdf",
                "application/pdf",
                true,
                new JsonObject { ["source"] = "integration-test" }),
            CancellationToken.None);

        Assert.Equal(PaperDocumentStatus.Pending, created.Status);
        Assert.True(created.RequiresOcr);
        Assert.Equal(paper.Id, created.PaperId);
        Assert.Equal("https://example.org/paper.pdf", created.SourceUrl);
    }

    [Fact]
    public async Task QueueProcessingAsync_sets_document_to_queued_and_creates_job()
    {
        await using var dbContext = CreateDbContext();
        var paper = new Paper { Title = "Queued paper" };
        var document = new PaperDocument
        {
            Paper = paper,
            SourceUrl = "https://example.org/queued.pdf",
            FileName = "queued.pdf",
            MediaType = "application/pdf",
            RequiresOcr = true
        };

        dbContext.Papers.Add(paper);
        dbContext.PaperDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var service = CreateDocumentService(dbContext);

        var queued = await service.QueueProcessingAsync(
            paper.Id,
            document.Id,
            new QueuePaperDocumentProcessingCommand("test-user"),
            CancellationToken.None);

        Assert.Equal(PaperDocumentStatus.Queued, queued.Status);

        var job = await dbContext.Jobs.SingleAsync();
        Assert.Equal(JobType.ProcessPaperDocument, job.Type);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal(document.Id, job.TargetEntityId);
        Assert.Equal("test-user", job.CreatedBy);
        Assert.Contains(document.Id.ToString(), job.PayloadJson);
    }

    [Fact]
    public async Task QueueProcessingAsync_when_already_queued_throws_conflict()
    {
        await using var dbContext = CreateDbContext();
        var paper = new Paper { Title = "Already queued paper" };
        var document = new PaperDocument
        {
            Paper = paper,
            SourceUrl = "https://example.org/already-queued.pdf",
            Status = PaperDocumentStatus.Queued
        };

        dbContext.Papers.Add(paper);
        dbContext.PaperDocuments.Add(document);
        await dbContext.SaveChangesAsync();

        var service = CreateDocumentService(dbContext);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.QueueProcessingAsync(
            paper.Id,
            document.Id,
            new QueuePaperDocumentProcessingCommand("test-user"),
            CancellationToken.None));

        Assert.Equal("Document processing is already queued.", ex.Message);
    }

    [Fact]
    public async Task ImportAsync_attaches_open_access_document_and_queues_processing_job()
    {
        await using var dbContext = CreateDbContext();
        var service = CreatePaperService(dbContext, new FakeSemanticScholarClient(new[]
        {
            new SemanticScholarPaperImportModel(
                "ss-1",
                "10.1000/test",
                "Imported paper",
                "Abstract",
                new[] { "Ada Lovelace" },
                2025,
                "TestConf",
                12,
                new JsonObject { ["openAccessPdfUrl"] = "https://example.org/open-access.pdf" })
        }));

        var result = await service.ImportAsync(new ImportPapersCommand(new[] { "paper" }, 10, true), CancellationToken.None);

        Assert.Single(result.Papers);

        var document = await dbContext.PaperDocuments.SingleAsync();
        Assert.Equal(PaperDocumentStatus.Queued, document.Status);
        Assert.Equal("https://example.org/open-access.pdf", document.SourceUrl);

        var job = await dbContext.Jobs.SingleAsync();
        Assert.Equal(JobType.ProcessPaperDocument, job.Type);
        Assert.Equal(document.Id, job.TargetEntityId);
        Assert.Equal(JobStatus.Queued, job.Status);
        Assert.Equal("system-import", job.CreatedBy);
    }

    private static PaperDocumentService CreateDocumentService(ApplicationDbContext dbContext)
    {
        var jobService = new JobService(dbContext, NullLogger<JobService>.Instance);
        return new PaperDocumentService(dbContext, jobService, NullLogger<PaperDocumentService>.Instance);
    }

    private static PaperService CreatePaperService(ApplicationDbContext dbContext, ISemanticScholarClient semanticScholarClient)
    {
        var jobService = new JobService(dbContext, NullLogger<JobService>.Instance);
        return new PaperService(dbContext, semanticScholarClient, jobService, NullLogger<PaperService>.Instance);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class FakeSemanticScholarClient(IReadOnlyCollection<SemanticScholarPaperImportModel> results) : ISemanticScholarClient
    {
        public Task<IReadOnlyCollection<SemanticScholarPaperImportModel>> SearchPapersAsync(
            IReadOnlyCollection<string> queries,
            int limit,
            CancellationToken cancellationToken) => Task.FromResult(results);
    }
}
