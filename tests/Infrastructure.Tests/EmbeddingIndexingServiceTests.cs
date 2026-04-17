using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests;

public sealed class EmbeddingIndexingServiceTests
{
    [Fact]
    public async Task UpsertPaperAbstractAsync_creates_embedding_for_paper_abstract()
    {
        await using var dbContext = CreateDbContext();
        var paper = new Paper
        {
            Title = "Test paper",
            Abstract = "  Paper abstract text  "
        };
        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var embeddingClient = new RecordingLocalEmbeddingClient(new[] { 0.1f, 0.2f, 0.3f });
        var service = CreateService(dbContext, embeddingClient);

        await service.UpsertPaperAbstractAsync(paper, CancellationToken.None);

        var embedding = await dbContext.PaperEmbeddings.SingleAsync();
        Assert.Equal(paper.Id, embedding.PaperId);
        Assert.Null(embedding.SummaryId);
        Assert.Equal(EmbeddingType.PaperAbstract, embedding.EmbeddingType);
        Assert.Equal("local-model", embedding.ModelName);
        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, embedding.Vector);
        Assert.Equal(new[] { "Paper abstract text" }, embeddingClient.Requests);
    }

    [Fact]
    public async Task UpsertPaperAbstractAsync_updates_existing_embedding_in_place()
    {
        await using var dbContext = CreateDbContext();
        var paper = new Paper
        {
            Title = "Existing paper",
            Abstract = "Initial abstract"
        };
        dbContext.Papers.Add(paper);

        var existingEmbedding = new PaperEmbedding
        {
            Paper = paper,
            EmbeddingType = EmbeddingType.PaperAbstract,
            ModelName = "old-model",
            Vector = new[] { 9f, 9f, 9f }
        };
        dbContext.PaperEmbeddings.Add(existingEmbedding);
        await dbContext.SaveChangesAsync();

        paper.Abstract = "Updated abstract";
        var embeddingClient = new RecordingLocalEmbeddingClient(new[] { 4.5f, 5.5f, 6.5f });
        var service = CreateService(dbContext, embeddingClient);

        await service.UpsertPaperAbstractAsync(paper, CancellationToken.None);

        var embeddings = await dbContext.PaperEmbeddings.ToListAsync();
        Assert.Single(embeddings);
        var embedding = embeddings[0];
        Assert.Equal(existingEmbedding.Id, embedding.Id);
        Assert.Equal(paper.Id, embedding.PaperId);
        Assert.Equal(EmbeddingType.PaperAbstract, embedding.EmbeddingType);
        Assert.Equal("local-model", embedding.ModelName);
        Assert.Equal(new[] { 4.5f, 5.5f, 6.5f }, embedding.Vector);
        Assert.Equal(new[] { "Updated abstract" }, embeddingClient.Requests);
    }

    [Fact]
    public async Task UpsertSummaryAsync_creates_embedding_for_summary_search_text()
    {
        await using var dbContext = CreateDbContext();
        var paper = new Paper { Title = "Summary paper" };
        var summary = new PaperSummary
        {
            Paper = paper,
            ModelName = "summarizer",
            PromptVersion = "v1",
            SearchText = "  Summary search text  "
        };
        dbContext.Papers.Add(paper);
        dbContext.PaperSummaries.Add(summary);
        await dbContext.SaveChangesAsync();

        var embeddingClient = new RecordingLocalEmbeddingClient(new[] { 7f, 8f, 9f });
        var service = CreateService(dbContext, embeddingClient);

        await service.UpsertSummaryAsync(summary, CancellationToken.None);

        var embedding = await dbContext.PaperEmbeddings.SingleAsync();
        Assert.Equal(paper.Id, embedding.PaperId);
        Assert.Equal(EmbeddingType.Summary, embedding.EmbeddingType);
        Assert.Equal("local-model", embedding.ModelName);
        Assert.Equal(new[] { 7f, 8f, 9f }, embedding.Vector);
        Assert.Equal(new[] { "Summary search text" }, embeddingClient.Requests);
    }

    private static EmbeddingIndexingService CreateService(ApplicationDbContext dbContext, ILocalEmbeddingClient embeddingClient)
    {
        var options = Options.Create(new LocalEmbeddingOptions { ModelName = "local-model", VectorDimensions = 3 });
        return new EmbeddingIndexingService(dbContext, embeddingClient, options, NullLogger<EmbeddingIndexingService>.Instance);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private sealed class RecordingLocalEmbeddingClient(params float[][] responses) : ILocalEmbeddingClient
    {
        private readonly Queue<float[]> _responses = new(responses);

        public List<string> Requests { get; } = [];

        public Task<float[]> GenerateEmbeddingAsync(string content, CancellationToken cancellationToken)
        {
            Requests.Add(content);
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : Array.Empty<float>());
        }
    }
}
