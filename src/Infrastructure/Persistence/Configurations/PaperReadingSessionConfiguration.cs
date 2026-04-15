using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class PaperReadingSessionConfiguration : IEntityTypeConfiguration<PaperReadingSession>
{
    public void Configure(EntityTypeBuilder<PaperReadingSession> builder)
    {
        builder.ToTable("paper_reading_sessions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(4096);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.PaperId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.UserId, x.Status });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Paper)
            .WithMany()
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
