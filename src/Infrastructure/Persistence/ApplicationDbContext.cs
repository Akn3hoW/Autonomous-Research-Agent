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
    public DbSet<UserNotificationPreferences> UserNotificationPreferences => Set<UserNotificationPreferences>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<PaperTag> PaperTags => Set<PaperTag>();
    public DbSet<AbTestSession> AbTestSessions => Set<AbTestSession>();
    public DbSet<ResearchGoalTemplate> ResearchGoalTemplates => Set<ResearchGoalTemplate>();

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

        modelBuilder.Entity<Tag>().HasIndex(t => t.Name);
        modelBuilder.Entity<Tag>().HasIndex(t => new { t.Name, t.UserId }).IsUnique();

        modelBuilder.Entity<PaperTag>().HasIndex(pt => pt.Tag);
        modelBuilder.Entity<PaperTag>().HasIndex(pt => new { pt.Tag, pt.PaperId, pt.UserId }).IsUnique();

        modelBuilder.Entity<PaperTag>()
            .HasOne(pt => pt.Paper)
            .WithMany(p => p.PaperTags)
            .HasForeignKey(pt => pt.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PaperTag>()
            .HasOne(pt => pt.User)
            .WithMany()
            .HasForeignKey(pt => pt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Tag>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AbTestSession>()
            .HasOne(a => a.Paper)
            .WithMany()
            .HasForeignKey(a => a.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AbTestSession>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PaperSummary>()
            .HasOne(s => s.AbTestSession)
            .WithMany(a => a.PaperSummaries)
            .HasForeignKey(s => s.AbTestSessionId)
            .OnDelete(DeleteBehavior.SetNull);
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
