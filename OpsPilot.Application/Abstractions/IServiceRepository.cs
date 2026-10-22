using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Abstractions;

public interface IServiceRepository
{
    Task<Guid> AddAsync(Service service, CancellationToken cancellationToken = default);

    Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Service service, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists services for the ambient tenant. Tenant scoping is applied by the global query
    /// filter, so there is deliberately no tenant parameter here.
    /// </summary>
    Task<(IReadOnlyList<Service> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// True when another service already claims <paramref name="name"/> inside
    /// <paramref name="environment"/>. The database enforces the same rule with a unique index
    /// on <c>(TenantId, Name, Environment)</c>; this check exists so the caller reports a clean
    /// 409 instead of an exception from the save path.
    /// </summary>
    Task<bool> NameExistsAsync(
        string name,
        EnvironmentType environment,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks a service up by the identity triple the ingestion API speaks in — name plus
    /// environment — rather than by id (plan, UC-06). Tenant scoping is applied by the global
    /// query filter.
    /// </summary>
    Task<Service?> GetByNameAsync(
        string name,
        EnvironmentType environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves, per service, the people who should hear about a service's incidents as its
    /// managers: the leads of the team that owns the service (plan, UC-12 follow-up).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the escalation target for a breach that has been left standing. The owner team
    /// is the service's stated owner (<c>Service.OwnerTeamId</c>), and its leads are the people
    /// accountable for it — the same team the ERD says an incident's service is paged through.
    /// </para>
    /// <para>
    /// Looked up for a whole batch of services in one query because the sweep examines a
    /// company's overdue incidents together. Services that no team owns, and teams with no
    /// leads, simply contribute no one; the caller keeps the direct recipients either way.
    /// </para>
    /// </remarks>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetServiceManagersAsync(
        IReadOnlyCollection<Guid> serviceIds,
        CancellationToken cancellationToken = default);
}
