using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.ConfigureAuditableEntity();

        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.NormalizedName).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.IsSystem).IsRequired();

        // ASP.NET Identity declares RoleNameIndex as unique on NormalizedName alone, which
        // makes role names globally unique -- impossible once every company has its own
        // "Viewer". Demote it to a plain lookup index and enforce uniqueness per company
        // (ERD §3) on the normalized value instead, so "Viewer" and "viewer" collide.
        builder.HasIndex(r => r.NormalizedName).HasDatabaseName("RoleNameIndex").IsUnique(false);
        builder.HasIndex(r => new { r.TenantId, r.NormalizedName }).IsUnique();
        builder.HasIndex(r => new { r.TenantId, r.IsSystem });

        builder.HasAlternateKey(r => new { r.TenantId, r.Id });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Permissions)
            .WithMany()
            .UsingEntity(join =>
            {
                join.ToTable("RolePermissions");
                join.Property<Guid>(ShadowProperties.TenantId).IsRequired();
                join.HasIndex(ShadowProperties.TenantId);
            });
    }
}
