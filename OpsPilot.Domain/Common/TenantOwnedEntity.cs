namespace OpsPilot.Domain.Common;

/// <summary>
/// Base class for every auditable, soft-deletable entity that belongs to a tenant.
/// </summary>
/// <remarks>
/// This intermediate type exists purely to capture the tenancy taxonomy: it lets the
/// persistence layer discover "all tenant-scoped rows" through a single base type,
/// while <see cref="BaseEntity"/> itself stays free of a tenant for the handful of
/// aggregates that are genuinely global (<see cref="Tenant"/>, <see cref="Permission"/>).
/// </remarks>
public abstract class TenantOwnedEntity : BaseEntity, ITenantOwned
{
    /// <summary>Identifier of the owning tenant. Set once, at construction time.</summary>
    public Guid TenantId { get; protected set; }
}
