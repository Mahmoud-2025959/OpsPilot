using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpsPilot.Domain.Common;

namespace OpsPilot.Infrastructure.Persistence.Configurations;

/// <summary>
/// Shared column mapping for the audit columns every entity inherits.
/// </summary>
/// <remarks>
/// This helper deliberately does <em>not</em> install a query filter. Query filters are
/// composed once, centrally, in <see cref="AppDbContext"/> so that the soft-delete and
/// tenant rules are applied together and cannot drift apart between entities.
/// </remarks>
public static class BaseEntityConfiguration
{
    /// <summary>Maps the audit columns for an entity that implements the audit contracts directly.</summary>
    public static void ConfigureAuditableEntity<T>(this EntityTypeBuilder<T> builder)
        where T : class, IAuditableEntity, ISoftDeletable
    {
        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(e => e.CreatedBy).HasMaxLength(128);
        builder.Property(e => e.UpdatedBy).HasMaxLength(128);
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.UpdatedAt);
    }

    /// <summary>Maps the audit columns inherited from <see cref="BaseEntity"/>.</summary>
    public static void ConfigureBaseEntity<T>(this EntityTypeBuilder<T> builder)
        where T : BaseEntity
    {
        builder.ConfigureAuditableEntity();
    }
}
