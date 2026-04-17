using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class CollectionPaperConfiguration : IEntityTypeConfiguration<CollectionPaper>
{
    public void Configure(EntityTypeBuilder<CollectionPaper> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("collection_papers");

        builder.HasKey(x => new { x.CollectionId, x.PaperId });

        builder.Property(x => x.SortOrder);

        builder.HasIndex(x => new { x.CollectionId, x.SortOrder });

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}