using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class PaperConfiguration : IEntityTypeConfiguration<Paper>
{
    public void Configure(EntityTypeBuilder<Paper> builder)
    {
        builder.ToTable("papers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SemanticScholarId).HasMaxLength(128);
        builder.HasIndex(x => x.SemanticScholarId);

        builder.Property(x => x.Doi).HasMaxLength(512);
        builder.HasIndex(x => x.Doi);

        builder.Property(x => x.Title).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Abstract);
        builder.Property(x => x.Authors).HasColumnType("text[]");
        builder.Property(x => x.Venue).HasMaxLength(256);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");

        builder.HasIndex(x => x.Title);
        builder.HasIndex(x => x.Year);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Source);

        builder.HasMany(x => x.Summaries)
            .WithOne(x => x.Paper)
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Embeddings)
            .WithOne(x => x.Paper)
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documents)
            .WithOne(x => x.Paper)
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Concepts)
            .WithOne(x => x.Paper)
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

