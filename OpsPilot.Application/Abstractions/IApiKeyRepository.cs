using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

public interface IApiKeyRepository
{
    Task<Guid> AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    Task<ApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists keys for the ambient tenant, newest first. Tenant scoping is applied by the global
    /// query filter, so there is deliberately no tenant parameter here.
    /// </summary>
    Task<(IReadOnlyList<ApiKey> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another key already claims <paramref name="name"/> in this tenant. The database
    /// enforces the same rule with a unique index on <c>(TenantId, Name)</c>.
    /// </summary>
    Task<bool> NameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when any tenant already uses <paramref name="keyPrefix"/>. The prefix alone is unique
    /// across the whole table — it is the one part of a key that is stored in the clear — so this
    /// check deliberately ignores the tenant filter and soft-deletes alike.
    /// </summary>
    Task<bool> KeyPrefixExistsAsync(string keyPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a key by the hash of the presented secret. Authentication happens before any tenant
    /// is known, so this crosses tenants on purpose: the hash is unique, and the tenant is then
    /// read back <em>from</em> the key (the same reasoning as the login-by-email reads).
    /// </summary>
    Task<ApiKey?> FindByKeyHashAcrossTenantsAsync(
        string keyHash,
        CancellationToken cancellationToken = default);
}
