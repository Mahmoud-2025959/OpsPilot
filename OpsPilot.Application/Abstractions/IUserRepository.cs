using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

public interface IUserRepository
{
    Task<Guid> AddAsync(User user, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<User> Items, int TotalCount)> GetPagedAsync(
        string? search,
        string? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(User user, CancellationToken cancellationToken = default);

    Task DeleteAsync(User user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Role>> GetRolesByNamesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken = default);

    // -- Authentication bootstrap reads -------------------------------------------------
    // Sign-in is by email alone and refresh tokens arrive without a bearer, so at those two
    // points no tenant is resolvable yet. These lookups therefore cross tenants on purpose:
    // both NormalizedEmail and User.Id are globally unique, so the isolation guarantee is
    // preserved by the identifier rather than by the filter (ERD §3).

    Task<User?> FindByEmailAcrossTenantsAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> EmailExistsAcrossTenantsAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> FindByIdAcrossTenantsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the account exists, is not deleted, and is still active.
    /// </summary>
    /// <remarks>
    /// A bearer token stays cryptographically valid until it expires, so this is what lets a
    /// deactivation or deletion take effect on the very next request instead of when the token
    /// happens to lapse. The lookup crosses tenants because the check runs before the tenant is
    /// resolved and <c>User.Id</c> is globally unique.
    /// </remarks>
    Task<bool> IsActiveAcrossTenantsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when this tenant still holds an active Owner other than <paramref name="excludedUserId"/>.
    /// </summary>
    /// <remarks>
    /// A company with no active Owner can never be administered again, so demoting or disabling
    /// the last one is refused. Both the role change and the status change paths consult this
    /// before they commit, which keeps the invariant in one place.
    /// </remarks>
    Task<bool> AnotherActiveOwnerExistsAsync(
        Guid excludedUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside a transaction holding an exclusive lock on this
    /// tenant's Owner guard, and returns its result.
    /// </summary>
    /// <remarks>
    /// "At least one active Owner" is a property of a <em>set</em> of rows, so no database
    /// constraint on a single row can state it. Two requests that each demote a different Owner
    /// could therefore both pass their own check and together leave the company with none. Every
    /// change that can alter the Owner set runs through here instead, so the second request
    /// re-reads the count only after the first has committed.
    /// </remarks>
    Task<T> ExecuteWithOwnerGuardAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="ExecuteWithOwnerGuardAsync{T}(Func{CancellationToken, Task{T}}, CancellationToken)" />
    Task ExecuteWithOwnerGuardAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
