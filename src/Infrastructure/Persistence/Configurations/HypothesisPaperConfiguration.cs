using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class HypothesisPaperConfiguration : IEntityTypeConfiguration<HypothesisPaper>
{
    public void Configure(EntityTypeBuilder<HypothesisPaper> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("hypothesis_papers");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.EvidenceType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.EvidenceText).HasMaxLength(4000);

        builder.HasIndex(x => x.HypothesisId);
        builder.HasIndex(x => x.PaperId);
        builder.HasIndex(x => new { x.HypothesisId, x.PaperId }).IsUnique();

        builder.HasOne(x => x.Hypothesis)
            .WithMany(x => x.HypothesisPapers)
            .HasForeignKey(x => x.HypothesisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
