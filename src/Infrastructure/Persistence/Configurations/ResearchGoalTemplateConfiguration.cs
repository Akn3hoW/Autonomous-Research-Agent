using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class ResearchGoalTemplateConfiguration : IEntityTypeConfiguration<ResearchGoalTemplate>
{
    public void Configure(EntityTypeBuilder<ResearchGoalTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("research_goal_templates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.GoalType).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.PromptTemplate).HasMaxLength(5000);
        builder.Property(x => x.Parameters).HasColumnType("jsonb");
        builder.Property(x => x.IsActive).HasDefaultValue(true);

        builder.HasIndex(x => x.IsActive);
    }
}