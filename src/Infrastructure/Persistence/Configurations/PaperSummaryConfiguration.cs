using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class PaperSummaryConfiguration : IEntityTypeConfiguration<PaperSummary>
{
    public void Configure(EntityTypeBuilder<PaperSummary> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("paper_summaries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ModelName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PromptVersion).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.SummaryJson).HasColumnType("jsonb");
        builder.Property(x => x.SearchText);
        builder.Property(x => x.ReviewedBy).HasMaxLength(256);
        builder.Property(x => x.ReviewNotes).HasMaxLength(2048);

        builder.HasIndex(x => x.PaperId);
        builder.HasIndex(x => x.Status);
    }
}

