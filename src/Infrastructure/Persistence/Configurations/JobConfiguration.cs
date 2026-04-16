using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ResultJson).HasColumnType("jsonb");
        builder.Property(x => x.ErrorMessage).HasMaxLength(4096);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.RetryPolicyJson).HasColumnType("jsonb");
        builder.Property(x => x.DependsOnJobIds).HasColumnType("jsonb");

        builder.HasOne(x => x.ParentJob)
            .WithMany(x => x.ChildJobs)
            .HasForeignKey(x => x.ParentJobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.TargetEntityId);
        builder.HasIndex(x => x.ParentJobId);
        builder.HasIndex(x => x.RetryCount);
        builder.HasIndex(x => x.LastAttemptAt);
        builder.HasIndex(x => x.CancellationRequestedAt);
        builder.Property(x => x.CancellationRequestedAt);
        builder.Property(x => x.TimeoutSeconds);
    }
}
