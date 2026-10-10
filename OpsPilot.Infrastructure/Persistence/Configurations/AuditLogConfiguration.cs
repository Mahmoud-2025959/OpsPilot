using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.ConfigureBaseEntity();

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.ActorUserId);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(200);
        builder.Property(a => a.EntityType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.EntityId).IsRequired().HasMaxLength(100);
        builder.Property(a => a.OldValues).HasMaxLength(4000);
        builder.Property(a => a.NewValues).HasMaxLength(4000);
        builder.Property(a => a.IpAddress).HasMaxLength(45);

        // Serves the tenant's audit view, which pages the newest entries first.
        builder.HasIndex(a => new { a.TenantId, a.CreatedAt });

        // Serves "show me the history of this one record".
        builder.HasIndex(a => new { a.TenantId, a.EntityType, a.EntityId });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Deliberately no foreign key on ActorUserId (ERD §4). The trail has to remain
        // readable and intact even after the acting user is disabled or removed, so the
        // reference is a bare id that nothing can invalidate.
    }
}
