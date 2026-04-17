using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class ResearchGapConfiguration : IEntityTypeConfiguration<ResearchGap>
{
    public void Configure(EntityTypeBuilder<ResearchGap> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("research_gaps");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Topic).HasMaxLength(512).IsRequired();
        builder.Property(x => x.GapAnalysisJson).HasColumnType("jsonb");
        builder.Property(x => x.CorpusCoverageJson).HasColumnType("jsonb");
        builder.Property(x => x.ExternalCoverageJson).HasColumnType("jsonb");
        builder.Property(x => x.SuggestedQueriesJson).HasColumnType("jsonb");
        builder.Property(x => x.CreatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Topic);
    }
}