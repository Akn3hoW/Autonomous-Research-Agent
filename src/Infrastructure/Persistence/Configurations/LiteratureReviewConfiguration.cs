using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class LiteratureReviewConfiguration : IEntityTypeConfiguration<LiteratureReview>
{
    public void Configure(EntityTypeBuilder<LiteratureReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("literature_reviews");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ResearchQuestion).HasMaxLength(4096).IsRequired();
        builder.Property(x => x.ContentJson).HasColumnType("jsonb");
        builder.Property(x => x.ContentMarkdown).HasColumnType("text");
        builder.Property(x => x.PaperIds).HasColumnType("integer[]");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.CompletedAt);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);
    }
}
