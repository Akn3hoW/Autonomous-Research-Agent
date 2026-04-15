using System.Text.Json;
using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AutonomousResearchAgent.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Paper> Papers => Set<Paper>();
    public DbSet<PaperSummary> PaperSummaries => Set<PaperSummary>();
    public DbSet<PaperEmbedding> PaperEmbeddings => Set<PaperEmbedding>();
    public DbSet<PaperDocument> PaperDocuments => Set<PaperDocument>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PaperCitation> PaperCitations => Set<PaperCitation>();
    public DbSet<PaperAnnotation> PaperAnnotations => Set<PaperAnnotation>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionPaper> CollectionPapers => Set<CollectionPaper>();
    public DbSet<TrendResult> TrendResults => Set<TrendResult>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Hypothesis> Hypotheses => Set<Hypothesis>();
    public DbSet<HypothesisPaper> HypothesisPapers => Set<HypothesisPaper>();
    public DbSet<PotentialDuplicate> PotentialDuplicates => Set<PotentialDuplicate>();
    public DbSet<LiteratureReview> LiteratureReviews => Set<LiteratureReview>();
    public DbSet<ResearchGap> ResearchGaps => Set<ResearchGap>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<PaperReadingSession> PaperReadingSessions => Set<PaperReadingSession>();
    public DbSet<PaperConcept> PaperConcepts => Set<PaperConcept>();
    public DbSet<UserWebhook> UserWebhooks => Set<UserWebhook>();
    public DbSet<BatchJob> BatchJobs => Set<BatchJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            modelBuilder.HasPostgresExtension("vector");
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.Entity<PaperEmbedding>().Property(e => e.Vector)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                s => s == null ? null : JsonSerializer.Deserialize<float[]>(s, (JsonSerializerOptions?)null));

        modelBuilder.Entity<User>().HasIndex(e => e.Email).IsUnique();

        if (Database.ProviderName != "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            modelBuilder.Entity<PaperEmbedding>().Ignore(x => x.Vector);
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInformation();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInformation();
        return base.SaveChanges();
    }

    private void ApplyAuditInformation()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
