using AutonomousResearchAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AutonomousResearchAgent.Infrastructure.Persistence.Configurations;

public sealed class UserWebhookConfiguration : IEntityTypeConfiguration<UserWebhook>
{
    public void Configure(EntityTypeBuilder<UserWebhook> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("user_webhooks");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Url)
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(w => w.Events)
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(w => w.Secret)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(w => w.IsActive)
            .HasDefaultValue(true);

        builder.HasIndex(w => w.UserId);
        builder.HasIndex(w => w.IsActive);
    }
}
