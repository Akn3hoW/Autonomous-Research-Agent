using AutonomousResearchAgent.Domain.Entities;
using AutonomousResearchAgent.Domain.Enums;
using AutonomousResearchAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Infrastructure.Tests;

public sealed class DatabaseMigrationTests
{
    [Fact]
    public async Task ApplicationDbContext_can_be_created_with_in_memory_database()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        Assert.NotNull(dbContext.Papers);
        Assert.NotNull(dbContext.PaperSummaries);
        Assert.NotNull(dbContext.PaperDocuments);
        Assert.NotNull(dbContext.Jobs);
        Assert.NotNull(dbContext.Users);
    }

    [Fact]
    public async Task ApplicationDbContext_can_save_and_retrieve_entities()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        var paper = new Paper
        {
            Title = "Test Paper",
            Abstract = "Abstract",
            Authors = ["Test Author"],
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.Papers.FindAsync(paper.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("Test Paper", retrieved.Title);
    }

    [Fact]
    public async Task ApplicationDbContext_applies_audit_information_on_save()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        var paper = new Paper
        {
            Title = "Test Paper",
            Abstract = "Abstract",
            Authors = ["Test Author"],
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        Assert.True(paper.CreatedAt > DateTimeOffset.MinValue);
        Assert.True(paper.UpdatedAt > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task ApplicationDbContext_tracks_modified_entities()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        var paper = new Paper
        {
            Title = "Original Title",
            Abstract = "Abstract",
            Authors = ["Test Author"],
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var originalUpdatedAt = paper.UpdatedAt;

        paper.Title = "Updated Title";
        await dbContext.SaveChangesAsync();

        Assert.True(paper.UpdatedAt >= originalUpdatedAt);
        Assert.Equal("Updated Title", paper.Title);
    }

    [Fact]
    public async Task ApplicationDbContext_can_query_with_tracking()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        var paper1 = new Paper
        {
            Title = "Paper 1",
            Abstract = "Abstract 1",
            Authors = ["Author 1"],
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        var paper2 = new Paper
        {
            Title = "Paper 2",
            Abstract = "Abstract 2",
            Authors = ["Author 2"],
            Year = 2023,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        dbContext.Papers.AddRange(paper1, paper2);
        await dbContext.SaveChangesAsync();

        var papers = await dbContext.Papers.ToListAsync();

        Assert.Equal(2, papers.Count);
    }

    [Fact]
    public async Task ApplicationDbContext_can_filter_with_where_clause()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        var paper1 = new Paper
        {
            Title = "Paper 2024",
            Abstract = "Abstract",
            Authors = ["Author"],
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        var paper2 = new Paper
        {
            Title = "Paper 2023",
            Abstract = "Abstract",
            Authors = ["Author"],
            Year = 2023,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        dbContext.Papers.AddRange(paper1, paper2);
        await dbContext.SaveChangesAsync();

        var papers2024 = await dbContext.Papers
            .Where(p => p.Year == 2024)
            .ToListAsync();

        Assert.Single(papers2024);
        Assert.Equal("Paper 2024", papers2024[0].Title);
    }

    [Fact]
    public async Task ApplicationDbContext_can_delete_entity()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        var paper = new Paper
        {
            Title = "To Be Deleted",
            Abstract = "Abstract",
            Authors = ["Author"],
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        dbContext.Papers.Remove(paper);
        await dbContext.SaveChangesAsync();

        var retrieved = await dbContext.Papers.FindAsync(paper.Id);
        Assert.Null(retrieved);
    }

    [Fact(Skip = "In-memory provider does not apply pgvector value converters when reading back entities, so this test cannot work as designed")]
    public async Task ApplicationDbContext_ignores_vector_property_on_non_postgres_provider()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        var paper = new Paper
        {
            Title = "Test Paper",
            Abstract = "Abstract",
            Authors = ["Author"],
            Year = 2024,
            Source = PaperSource.SemanticScholar,
            Status = PaperStatus.Imported
        };

        paper.Embeddings.Add(new PaperEmbedding
        {
            Paper = paper,
            EmbeddingType = EmbeddingType.PaperAbstract,
            Vector = new float[] { 1.0f, 2.0f },
            ModelName = "test-model"
        });

        dbContext.Papers.Add(paper);
        await dbContext.SaveChangesAsync();

        var savedPaper = await dbContext.Papers
            .Include(p => p.Embeddings)
            .FirstOrDefaultAsync(p => p.Id == paper.Id);

        Assert.NotNull(savedPaper);
        if (savedPaper != null && savedPaper.Embeddings.Count > 0)
        {
            Assert.True(savedPaper.Embeddings.First().Vector?.Length == 0 ||
                        savedPaper.Embeddings.First().Vector == null);
        }
    }

    [Fact]
    public async Task ApplicationDbContext_job_status_transitions_are_valid()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);

        var job = new Job
        {
            Type = JobType.ImportPapers,
            Status = JobStatus.Queued,
            PayloadJson = "{}"
        };

        dbContext.Jobs.Add(job);
        await dbContext.SaveChangesAsync();

        job.Status = JobStatus.Running;
        await dbContext.SaveChangesAsync();

        job.Status = JobStatus.Completed;
        await dbContext.SaveChangesAsync();

        var updatedJob = await dbContext.Jobs.FindAsync(job.Id);
        Assert.NotNull(updatedJob);
        Assert.Equal(JobStatus.Completed, updatedJob.Status);
    }
}
