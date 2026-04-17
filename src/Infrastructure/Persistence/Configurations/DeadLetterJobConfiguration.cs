using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class DeadLetterJobConfiguration : IEntityTypeConfiguration<DeadLetterJob>
{
    public void Configure(EntityTypeBuilder<DeadLetterJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("dead_letter_jobs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.OriginalJobType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.OriginalJobPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(4096);
        builder.Property(x => x.ExceptionType).HasMaxLength(256);
        builder.Property(x => x.StackTrace).HasColumnType("text");
        builder.Property(x => x.ProcessingNotes).HasMaxLength(512);

        builder.HasOne(x => x.OriginalJob)
            .WithMany()
            .HasForeignKey(x => x.OriginalJobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.OriginalJobId);
        builder.HasIndex(x => x.FailedAt);
        builder.HasIndex(x => x.IsProcessed);
        builder.HasIndex(x => x.ExceptionType);
    }
}
