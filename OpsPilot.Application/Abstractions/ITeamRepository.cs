using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

public interface ITeamRepository
{
    Task<Guid> AddAsync(Team team, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a team together with its members. The membership rules (<c>AddMember</c>,
    /// <c>RemoveMember</c>) inspect the loaded collection, so callers must not use a
    /// members-less team to mutate membership.
    /// </summary>
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Team team, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists teams for the ambient tenant with their members. Tenant scoping is applied by the
    /// global query filter, so there is deliberately no tenant parameter here.
    /// </summary>
    Task<(IReadOnlyList<Team> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another team already claims <paramref name="name"/> in this tenant. The
    /// database enforces the same rule with a unique index on <c>(TenantId, Name)</c>.
    /// </summary>
    Task<bool> NameExistsAsync(
        string name,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);
}
