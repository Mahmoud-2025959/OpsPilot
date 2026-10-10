using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.ConfigureBaseEntity();

        builder.Property(n => n.TenantId).IsRequired();
        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.IncidentId);
        builder.Property(n => n.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(300);
        builder.Property(n => n.Body).IsRequired().HasMaxLength(4000);
        builder.Property(n => n.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(n => n.SentAt);
        builder.Property(n => n.ReadAt);

        // Backs the unread badge: a user's notifications ordered by whether they are read.
        builder.HasIndex(n => new { n.TenantId, n.UserId, n.ReadAt });
        builder.HasIndex(n => n.TenantId);

        builder.Ignore(n => n.IsRead);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(n => n.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(n => n.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
