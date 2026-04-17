using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class PaperAnnotationConfiguration : IEntityTypeConfiguration<PaperAnnotation>
{
    public void Configure(EntityTypeBuilder<PaperAnnotation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("paper_annotations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.HighlightedText).HasMaxLength(65535).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(4096);
        builder.Property(x => x.PageNumber);
        builder.Property(x => x.OffsetStart);
        builder.Property(x => x.OffsetEnd);

        builder.HasIndex(x => x.PaperId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.DocumentChunkId);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DocumentChunk)
            .WithMany()
            .HasForeignKey(x => x.DocumentChunkId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}