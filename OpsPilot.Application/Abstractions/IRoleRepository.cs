using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// The roles of the ambient company (ERD §3).
/// </summary>
/// <remarks>
/// Roles are tenant-scoped, so no method here takes a tenant id: the global query filter has
/// already narrowed the set to the company the request was resolved for.
/// </remarks>
public interface IRoleRepository
{
    /// <summary>Lists the company's roles, each with the permissions it grants.</summary>
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a role together with the permissions it grants. The permission set is mutated
    /// through the loaded collection, so a role loaded without it must not be used to change
    /// what it grants.
    /// </summary>
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another role already claims <paramref name="name"/> in this company. The
    /// database enforces the same rule with a unique index on <c>(TenantId, NormalizedName)</c>.
    /// </summary>
    Task<bool> NameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);

    Task<Guid> AddAsync(Role role, CancellationToken cancellationToken = default);

    Task UpdateAsync(Role role, CancellationToken cancellationToken = default);

    Task<bool> HasUsersAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Retires a role. The row is kept so the users who held it stay resolvable.</summary>
    Task DeleteAsync(Role role, CancellationToken cancellationToken = default);
}
