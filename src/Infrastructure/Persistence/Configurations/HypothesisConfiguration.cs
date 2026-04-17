using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class HypothesisConfiguration : IEntityTypeConfiguration<Hypothesis>
{
    public void Configure(EntityTypeBuilder<Hypothesis> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("hypotheses");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.SupportingEvidenceJson).HasColumnType("jsonb");
        builder.Property(x => x.RefutingEvidenceJson).HasColumnType("jsonb");

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.Status);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
