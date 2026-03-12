using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class PaperEmbeddingConfiguration : IEntityTypeConfiguration<PaperEmbedding>
{
    private const int DefaultEmbeddingDimensions = 1536;

    public void Configure(EntityTypeBuilder<PaperEmbedding> builder)
    {
        builder.ToTable("paper_embeddings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EmbeddingType).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.ModelName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Vector)
            .HasConversion(
                value => value == null ? null : new Vector(value),
                value => value == null ? null : value.ToArray())
            .HasColumnType($"vector({DefaultEmbeddingDimensions})");

        builder.HasIndex(x => x.PaperId);
        builder.HasIndex(x => x.SummaryId);
        builder.HasIndex(x => x.EmbeddingType);

        builder.HasOne(x => x.Summary)
            .WithMany(x => x.Embeddings)
            .HasForeignKey(x => x.SummaryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
