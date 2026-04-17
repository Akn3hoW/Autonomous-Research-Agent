using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class PotentialDuplicateConfiguration : IEntityTypeConfiguration<PotentialDuplicate>
{
    public void Configure(EntityTypeBuilder<PotentialDuplicate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("potential_duplicates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SimilarityScore).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.Notes).HasMaxLength(2048);

        builder.HasOne(x => x.PaperA)
            .WithMany()
            .HasForeignKey(x => x.PaperAId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.PaperB)
            .WithMany()
            .HasForeignKey(x => x.PaperBId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReviewedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.PaperAId);
        builder.HasIndex(x => x.PaperBId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.PaperAId, x.PaperBId }).IsUnique();
    }
}
