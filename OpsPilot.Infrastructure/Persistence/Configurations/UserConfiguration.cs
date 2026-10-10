using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.ConfigureAuditableEntity();

        builder.Property(u => u.TenantId).IsRequired();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.NormalizedEmail).IsRequired().HasMaxLength(320);
        builder.Property(u => u.UserName).IsRequired().HasMaxLength(320);
        builder.Property(u => u.NormalizedUserName).IsRequired().HasMaxLength(320);
        builder.Property(u => u.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.PasswordHash).HasMaxLength(512);
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.LastLoginAt);

        // ERD §3: sign-in is by email alone, with no tenant name in the credential, so the
        // address has to identify exactly one account across the whole system.
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();

        // Every tenant-scoped query filter leads with TenantId, so the index must too.
        builder.HasIndex(u => u.TenantId);

        // Lets the database refuse a UserRole row whose user belongs to a different tenant
        // than the row itself (ERD §3, "قواعد الأدوار المخصصة").
        builder.HasAlternateKey(u => new { u.TenantId, u.Id });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.Roles)
            .WithMany()
            .UsingEntity<IdentityUserRole<Guid>>(
                l => l.HasOne<Role>()
                    .WithMany()
                    .HasForeignKey(e => e.RoleId)
                    .OnDelete(DeleteBehavior.Cascade),
                r => r.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade),
                j =>
                {
                    j.ToTable("UserRoles");
                    j.HasKey(ur => new { ur.UserId, ur.RoleId });

                    // Identity builds these rows without any tenant awareness, so TenantId is
                    // a shadow property filled in by AuditingSaveChangesInterceptor.
                    j.Property<Guid>(ShadowProperties.TenantId).IsRequired();
                    j.HasIndex(ShadowProperties.TenantId);
                });
    }
}
