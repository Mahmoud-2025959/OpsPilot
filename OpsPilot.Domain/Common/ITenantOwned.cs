namespace OpsPilot.Domain.Common;

/// <summary>
/// Marks an entity whose rows belong to exactly one tenant.
/// </summary>
/// <remarks>
/// The persistence layer turns every implementation into a single global query filter,
/// so reading across tenants is impossible by construction rather than by convention
/// (ERD §1). Only <see cref="Tenant"/>, <see cref="Permission"/> and the infrastructure
/// tables deliberately opt out.
/// </remarks>
public interface ITenantOwned
{
    /// <summary>Identifier of the tenant that owns the row.</summary>
    Guid TenantId { get; }
}
