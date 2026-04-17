using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("document_chunks");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Text).IsRequired();
        builder.Property(x => x.TextLength);

        builder.HasIndex(x => x.PaperDocumentId);
        builder.HasIndex(x => new { x.PaperDocumentId, x.ChunkIndex }).IsUnique();

        builder.HasOne(x => x.PaperDocument)
            .WithMany(x => x.Chunks)
            .HasForeignKey(x => x.PaperDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
