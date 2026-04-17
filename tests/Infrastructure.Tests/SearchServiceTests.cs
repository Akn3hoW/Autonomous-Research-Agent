using AutonomousResearchAgent.Application.Common;
using AutonomousResearchAgent.Application.Search;
using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using AutonomousResearchAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Infrastructure.Tests;

public sealed class SearchServiceTests
{
    private static readonly IOptions<SearchWeightsOptions> DefaultSearchWeights = Options.Create(new SearchWeightsOptions
    {
        Title = 1.0,
        Abstract = 0.6,
        Summary = 0.4,
        Document = 0.5,
        RrfConstantK = 60
    });

    [Fact(Skip = "Requires PostgreSQL - LoadSemanticCandidatesAsync uses LINQ expressions that in-memory provider cannot translate")]
    public async Task SemanticSearchAsync_ranks_papers_using_best_embedding_across_abstract_and_summary_vectors()
    {
        var dbContextOptions = CreateDbContextOptions();
        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        var queryEmbeddingService = new FakeEmbeddingService(new[] { 1f, 0f });
        var service = CreateSearchService(dbContextOptions, queryEmbeddingService);

        var paperWithStrongSummary = CreatePaper(
            "Paper with strong summary",
            abstractEmbedding: new[] { 0.25f, 0.97f },
            summaryEmbedding: new[] { 1f, 0f });

        var paperWithModerateAbstract = CreatePaper(
            "Paper with moderate abstract",
            abstractEmbedding: new[] { 0.8f, 0.6f });

        dbContext.Papers.AddRange(paperWithStrongSummary, paperWithModerateAbstract);
        await dbContext.SaveChangesAsync();

        var result = await service.SemanticSearchAsync(
            new SemanticSearchRequestModel("semantic query", PageNumber: 1, PageSize: 10, MaxCandidates: 10),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(paperWithStrongSummary.Id, result.Items.First().PaperId);
        Assert.Equal("semantic", result.Items.First().MatchType);
        Assert.Equal(1.0, result.Items.First().Score, 5);
        Assert.Equal(paperWithModerateAbstract.Id, result.Items.Last().PaperId);
        Assert.Equal(0.8, result.Items.Last().Score, 5);
    }

    [Fact(Skip = "Requires PostgreSQL - HybridSearchAsync uses LoadSemanticCandidatesAsync which has LINQ expressions that in-memory provider cannot translate")]
    public async Task HybridSearchAsync_uses_rrf_to_fuse_keyword_and_semantic_rankings()
    {
        var dbContextOptions = CreateDbContextOptions();
        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        var queryEmbeddingService = new FakeEmbeddingService(new[] { 1f, 0f });
        var service = CreateSearchService(dbContextOptions, queryEmbeddingService);

        var keywordAndSemanticPaper = CreatePaper(
            "Quantum paper",
            abstractEmbedding: new[] { 0.2f, 0.98f },
            abstractText: "General relativity abstract");

        var semanticOnlyPaper = CreatePaper(
            "Unrelated paper",
            abstractEmbedding: new[] { 1f, 0f },
            abstractText: "Completely unrelated abstract");

        dbContext.Papers.AddRange(keywordAndSemanticPaper, semanticOnlyPaper);
        await dbContext.SaveChangesAsync();

        var result = await service.HybridSearchAsync(
            new HybridSearchRequestModel(
                Query: "Quantum",
                KeywordWeight: 0.25,
                SemanticWeight: 0.75,
                PageNumber: 1,
                PageSize: 10,
                MaxCandidates: 10),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        var topResult = result.Items.First();
        var secondResult = result.Items.Last();

        Assert.Equal("hybrid", topResult.MatchType);
        Assert.Equal("hybrid", secondResult.MatchType);

        var topHighlights = Assert.IsType<System.Text.Json.Nodes.JsonObject>(topResult.Highlights);
        var secondHighlights = Assert.IsType<System.Text.Json.Nodes.JsonObject>(secondResult.Highlights);

        Assert.NotNull(topHighlights["keywordRrfScore"]);
        Assert.NotNull(topHighlights["semanticRrfScore"]);
        Assert.NotNull(secondHighlights["keywordRrfScore"]);
        Assert.NotNull(secondHighlights["semanticRrfScore"]);
    }

    [Fact(Skip = "Requires PostgreSQL - HybridSearchAsync uses LoadSemanticCandidatesAsync which has LINQ expressions that in-memory provider cannot translate")]
    public async Task HybridSearchAsync_with_ranked_results_computes_rrf_scores_correctly()
    {
        var dbContextOptions = CreateDbContextOptions();
        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        var queryEmbeddingService = new FakeEmbeddingService(new[] { 1f, 0f });
        var service = CreateSearchService(dbContextOptions, queryEmbeddingService);

        var rank1Paper = CreatePaper("First paper", abstractEmbedding: new[] { 1f, 0f });
        var rank2Paper = CreatePaper("Second paper", abstractEmbedding: new[] { 0.9f, 0.1f });
        var rank3Paper = CreatePaper("Third paper", abstractEmbedding: new[] { 0.8f, 0.2f });

        dbContext.Papers.AddRange(rank1Paper, rank2Paper, rank3Paper);
        await dbContext.SaveChangesAsync();

        var result = await service.HybridSearchAsync(
            new HybridSearchRequestModel(
                Query: "test",
                PageNumber: 1,
                PageSize: 10,
                MaxCandidates: 10),
            CancellationToken.None);

        Assert.Equal(3, result.TotalCount);

        var topHighlights = Assert.IsType<System.Text.Json.Nodes.JsonObject>(result.Items.First().Highlights);
        var secondHighlights = Assert.IsType<System.Text.Json.Nodes.JsonObject>(result.Items.ElementAt(1).Highlights);

        var topKeywordRrf = topHighlights["keywordRrfScore"]?.GetValue<double>() ?? 0;
        var secondKeywordRrf = secondHighlights["keywordRrfScore"]?.GetValue<double>() ?? 0;

        Assert.True(topKeywordRrf > secondKeywordRrf);
    }

    [Fact(Skip = "Requires PostgreSQL - SearchChunksInMemoryAsync uses complex include/where that in-memory provider cannot translate")]
    public async Task SearchDocumentChunksAsync_returns_chunk_results()
    {
        var dbContextOptions = CreateDbContextOptions();
        await using var dbContext = new ApplicationDbContext(dbContextOptions);
        var queryEmbeddingService = new FakeEmbeddingService(new[] { 1f, 0f });
        var service = CreateSearchService(dbContextOptions, queryEmbeddingService);

        var paper = new Paper
        {
            Id = Guid.NewGuid(),
            Title = "Test Paper",
            Abstract = "Test abstract",
            Authors = ["Author"],
            Year = 2025
        };

        var document = new PaperDocument
        {
            Id = Guid.NewGuid(),
            PaperId = paper.Id,
            ExtractedText = "Some document text"
        };

        var chunk = new DocumentChunk
        {
            Id = Guid.NewGuid(),
            PaperDocumentId = document.Id,
            Text = "Chunk text content",
            ChunkIndex = 0,
            TextLength = 18,
            StartPosition = 0,
            EndPosition = 18
        };

        var chunkEmbedding = new PaperEmbedding
        {
            Id = Guid.NewGuid(),
            PaperId = paper.Id,
            DocumentChunkId = chunk.Id,
            EmbeddingType = EmbeddingType.DocumentChunk,
            Vector = new[] { 1f, 0f },
            ModelName = "test-model",
            VectorDimensions = 2
        };

        dbContext.Papers.Add(paper);
        dbContext.PaperDocuments.Add(document);
        dbContext.DocumentChunks.Add(chunk);
        dbContext.PaperEmbeddings.Add(chunkEmbedding);
        await dbContext.SaveChangesAsync();

        var result = await service.SearchDocumentChunksAsync(
            new ChunkSearchRequestModel("chunk query", PageNumber: 1, PageSize: 10, MaxCandidates: 10),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(chunk.Id, result.Items.First().ChunkId);
        Assert.Equal("Chunk text content", result.Items.First().ChunkText);
        Assert.Equal(paper.Id, result.Items.First().PaperId);
    }

    private static SearchService CreateSearchService(DbContextOptions<ApplicationDbContext> dbContextOptions, IEmbeddingService embeddingService)
    {
        return new SearchService(
            new TestDbContextFactory(dbContextOptions),
            embeddingService,
            DefaultSearchWeights,
            NullLogger<SearchService>.Instance);
    }

    private static DbContextOptions<ApplicationDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(CreateDbContextOptions());
    }

    private sealed class TestDbContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        {
            _options = options;
        }

        public ApplicationDbContext CreateDbContext() => new(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new ApplicationDbContext(_options));
    }

    private static Paper CreatePaper(
        string title,
        float[] abstractEmbedding,
        float[]? summaryEmbedding = null,
        string? abstractText = null)
    {
        var paper = new Paper
        {
            Id = Guid.NewGuid(),
            Title = title,
            Abstract = abstractText ?? $"{title} abstract",
            Authors = ["Test Author"],
            Year = 2025,
            Venue = "Test Venue"
        };

        paper.Embeddings.Add(new PaperEmbedding
        {
            Id = Guid.NewGuid(),
            Paper = paper,
            EmbeddingType = EmbeddingType.PaperAbstract,
            Vector = abstractEmbedding,
            ModelName = "test-model",
            VectorDimensions = abstractEmbedding.Length
        });

        if (summaryEmbedding is not null)
        {
            var summary = new PaperSummary
            {
                Id = Guid.NewGuid(),
                Paper = paper,
                SearchText = $"{title} summary"
            };

            paper.Summaries.Add(summary);
            paper.Embeddings.Add(new PaperEmbedding
            {
                Id = Guid.NewGuid(),
                Paper = paper,
                Summary = summary,
                EmbeddingType = EmbeddingType.Summary,
                Vector = summaryEmbedding,
                ModelName = "test-model",
                VectorDimensions = summaryEmbedding.Length
            });
        }

        return paper;
    }

    private sealed class FakeEmbeddingService(float[] queryEmbedding) : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string content, EmbeddingType embeddingType, CancellationToken cancellationToken)
            => Task.FromResult(queryEmbedding);

        public Task<float[]> GenerateQueryEmbeddingAsync(string query, CancellationToken cancellationToken)
            => Task.FromResult(queryEmbedding);
    }
}
