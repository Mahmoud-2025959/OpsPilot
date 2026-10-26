using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// The company a request belongs to (ERD §1).
/// </summary>
/// <remarks>
/// <see cref="Entities.Tenant"/> is the one row that is not scoped by itself — it is what
/// scoping is built on — so these lookups deliberately cross the tenant boundary. Everything
/// else in the application reaches the company through <see cref="ICurrentTenant"/> instead.
/// </remarks>
public interface ITenantRepository
{
    /// <summary>Loads the company, or null when no such company exists.</summary>
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Saves a change made to the company.</summary>
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken = default);
}
