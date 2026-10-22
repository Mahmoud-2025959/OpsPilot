using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// The result of provisioning a brand-new tenant: the company itself plus the Owner role it
/// was seeded with, so the caller can immediately promote the signing-up user.
/// </summary>
public sealed record ProvisionedTenant(Tenant Tenant, Role OwnerRole);

/// <summary>
/// Creates a company together with the rows every company needs to function: the five
/// system roles, their permissions, and the default SLA policies (ERD §3).
/// </summary>
/// <remarks>
/// This is the "sign-up creates the tenant" half of the plan. It is an explicit abstraction
/// rather than a side effect of registration so that the same provisioning can be reused by
/// an admin flow or a seeding tool without going through the public API.
/// </remarks>
public interface ITenantProvisioner
{
    Task<ProvisionedTenant> ProvisionAsync(
        string companyName,
        string? slug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the global permission catalogue exists. Idempotent: safe to call on every
    /// provisioning and from a seeding tool.
    /// </summary>
    Task EnsurePermissionCatalogAsync(CancellationToken cancellationToken = default);
}
