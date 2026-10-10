namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Exposes the tenant the current unit of work belongs to.
/// </summary>
/// <remarks>
/// The persistence layer feeds this into a global query filter, so "which tenant am I?"
/// is answered in exactly one place instead of being threaded through every repository.
/// Reading is fail-closed: while <see cref="IsResolved"/> is false, tenant-scoped queries
/// return nothing. Genuinely cross-tenant work must opt out explicitly via EF's
/// <c>IgnoreQueryFilters()</c>, so the exception is always visible in the code that makes it.
/// </remarks>
public interface ICurrentTenant
{
    /// <summary>The current tenant, or <see cref="Guid.Empty"/> when unresolved.</summary>
    Guid TenantId { get; }

    /// <summary>True once a tenant has been established for the current scope.</summary>
    bool IsResolved { get; }

    /// <summary>
    /// Establishes <paramref name="tenantId"/> for the duration of the returned scope and
    /// restores the previous value on dispose. Used by background jobs, which have no
    /// ambient request to inherit a tenant from.
    /// </summary>
    IDisposable Enter(Guid tenantId);
}
