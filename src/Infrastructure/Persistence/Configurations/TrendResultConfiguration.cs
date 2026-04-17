using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class TrendResultConfiguration : IEntityTypeConfiguration<TrendResult>
{
    public void Configure(EntityTypeBuilder<TrendResult> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("trend_results");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Field).HasMaxLength(256);
        builder.Property(x => x.StartYear);
        builder.Property(x => x.EndYear);
        builder.Property(x => x.ResultJson).HasColumnType("jsonb");
        builder.Property(x => x.CalculatedAt);

        builder.HasIndex(x => new { x.Field, x.StartYear, x.EndYear });
    }
}