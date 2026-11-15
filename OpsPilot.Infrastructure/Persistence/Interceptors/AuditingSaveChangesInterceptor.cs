using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Common;

namespace OpsPilot.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps audit metadata on every tracked change and back-fills the tenant on the ASP.NET
/// Identity join tables, which Identity writes without knowing about tenancy.
/// </summary>
/// <remarks>
/// This lives in an interceptor rather than in <c>SaveChanges</c> so that audit stamping
/// applies uniformly regardless of which of the three save paths (sync, async, batch) the
/// caller uses, and so it can be reasoned about — and unit tested — in isolation.
/// </remarks>
public sealed class AuditingSaveChangesInterceptor(
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    private const string ShadowTenantId = "TenantId";

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            ApplyAudit(context);
            StampJoinTableTenants(context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            ApplyAudit(context);
            StampJoinTableTenants(context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext context)
    {
        var now = timeProvider.GetUtcNow();
        var actor = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy ??= actor;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = actor;
                    break;
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.IsDeleted = false;
            }
        }
    }

    /// <summary>
    /// Fills the shadow <c>TenantId</c> on the Identity join tables. Identity's own
    /// <c>UserManager</c> APIs construct these rows without a tenant, so the value has to
    /// be derived here rather than supplied by the caller.
    /// </summary>
    private void StampJoinTableTenants(DbContext context)
    {
        var pending = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added && e.Metadata.FindProperty(ShadowTenantId) is not null)
            .ToList();

        foreach (var entry in pending)
        {
            var property = entry.Property(ShadowTenantId);

            if (property.CurrentValue is Guid existing && existing != Guid.Empty)
            {
                continue;
            }

            var tenantId = ResolvePrincipalTenant(context, entry)
                ?? (currentTenant.IsResolved ? currentTenant.TenantId : null);

            if (tenantId is not { } resolved || resolved == Guid.Empty)
            {
                throw new InvalidOperationException(
                    $"Could not determine the tenant for '{entry.Metadata.ClrType.Name}'. " +
                    "No tenant-owned principal was found and no tenant is resolved for the current scope.");
            }

            property.CurrentValue = resolved;
        }
    }

    /// <summary>
    /// Derives the tenant from a tenant-owned principal referenced by one of the entry's
    /// foreign keys, preferring the change tracker over a database round trip.
    /// </summary>
    private static Guid? ResolvePrincipalTenant(DbContext context, EntityEntry entry)
    {
        foreach (var foreignKey in entry.Metadata.GetForeignKeys())
        {
            var principalClrType = foreignKey.PrincipalEntityType.ClrType;

            if (!typeof(ITenantOwned).IsAssignableFrom(principalClrType))
            {
                continue;
            }

            // Identity join tables are single-column foreign keys. Read the value from the
            // dependent's own FK property -- on a join entry it is named after the navigation
            // (e.g. RolesId), not after the principal key it points at -- and use the principal
            // key name only to read the principal's value.
            if (foreignKey.Properties.Count != 1 || foreignKey.PrincipalKey.Properties.Count != 1)
            {
                continue;
            }

            var dependentPropertyName = foreignKey.Properties[0].Name;

            if (entry.Property(dependentPropertyName).CurrentValue is not Guid { } id || id == Guid.Empty)
            {
                continue;
            }

            var principalKeyName = foreignKey.PrincipalKey.Properties[0].Name;

            var tracked = context.ChangeTracker.Entries()
                .Where(e => e.Metadata.ClrType == principalClrType)
                .Select(e => e.Entity)
                .FirstOrDefault(entity => id.Equals(ReadKey(entity, principalKeyName)));

            if (tracked is ITenantOwned trackedOwned)
            {
                return trackedOwned.TenantId;
            }

            // Prefer the change tracker, then fall back to the store via Find, which also
            // checks the tracker and honours the tenant filter. A join row must never be
            // stamped for a tenant the current scope cannot see.
            var loaded = context.Find(principalClrType, id);

            if (loaded is ITenantOwned fromStore)
            {
                return fromStore.TenantId;
            }
        }

        return null;
    }

    private static object? ReadKey(object entity, string propertyName)
        => entity.GetType().GetProperty(propertyName)?.GetValue(entity);
}
