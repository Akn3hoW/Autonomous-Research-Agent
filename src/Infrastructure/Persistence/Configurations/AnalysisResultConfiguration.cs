using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class AnalysisResultConfiguration : IEntityTypeConfiguration<AnalysisResult>
{
    public void Configure(EntityTypeBuilder<AnalysisResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("analysis_results");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.AnalysisType).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.InputSetJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ResultJson).HasColumnType("jsonb");
        builder.Property(x => x.CreatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.AnalysisType);
        builder.HasIndex(x => x.JobId).IsUnique();

        builder.HasOne(x => x.Job)
            .WithMany()
            .HasForeignKey(x => x.JobId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

