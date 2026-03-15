using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class PaperDocumentConfiguration : IEntityTypeConfiguration<PaperDocument>
{
    public void Configure(EntityTypeBuilder<PaperDocument> builder)
    {
        builder.ToTable("paper_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SourceUrl).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(512);
        builder.Property(x => x.MediaType).HasMaxLength(256);
        builder.Property(x => x.StoragePath).HasMaxLength(2048);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");
        builder.Property(x => x.LastError).HasMaxLength(4096);
        builder.Property(x => x.ExtractedText);

        builder.HasIndex(x => x.PaperId);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.Paper)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
