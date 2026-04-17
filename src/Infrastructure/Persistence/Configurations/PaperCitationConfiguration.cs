using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class PaperCitationConfiguration : IEntityTypeConfiguration<PaperCitation>
{
    public void Configure(EntityTypeBuilder<PaperCitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("paper_citations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CitationContext);
        builder.Property(x => x.IngestedAt);

        builder.HasIndex(x => x.SourcePaperId);
        builder.HasIndex(x => x.TargetPaperId);
        builder.HasIndex(x => new { x.SourcePaperId, x.TargetPaperId }).IsUnique();

        builder.HasOne(x => x.SourcePaper)
            .WithMany()
            .HasForeignKey(x => x.SourcePaperId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TargetPaper)
            .WithMany()
            .HasForeignKey(x => x.TargetPaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
