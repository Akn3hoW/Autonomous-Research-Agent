using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class BatchJobConfiguration : IEntityTypeConfiguration<BatchJob>
{
    public void Configure(EntityTypeBuilder<BatchJob> builder)
    {
        builder.ToTable("batch_jobs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(x => x.Total).IsRequired();
        builder.Property(x => x.Completed).IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
    }
}