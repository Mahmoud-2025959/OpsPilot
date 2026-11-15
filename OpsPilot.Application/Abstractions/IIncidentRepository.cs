using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Abstractions;

public interface IIncidentRepository
{
    Task<Guid> AddAsync(Incident incident, CancellationToken cancellationToken = default);

    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists incidents for the ambient tenant, newest first. Tenant scoping is applied by the
    /// global query filter, so there is deliberately no tenant parameter here.
    /// </summary>
    Task<(IReadOnlyList<Incident> Items, int TotalCount)> GetPagedAsync(
        IncidentStatus? status,
        Severity? severity,
        Guid? serviceId,
        Guid? assignedTo,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddCommentAsync(IncidentComment comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentComment>> GetCommentsAsync(Guid incidentId, CancellationToken cancellationToken = default);

    Task AddEventAsync(IncidentEvent incidentEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends the timeline entry and the integration message that announces it in one save
    /// (plan, UC-06 step 7).
    /// </summary>
    /// <remarks>
    /// The pairing is what keeps the outbox honest: an entry and the message describing it
    /// commit together or not at all, so a published message always points at a stored entry.
    /// </remarks>
    Task AddEventAsync(
        IncidentEvent incidentEvent,
        OutboxMessage integrationMessage,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentEvent>> GetEventsAsync(Guid incidentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the incident an incoming event should join: the one open incident (anything other
    /// than Resolved or Closed) carrying <paramref name="correlationKey"/> in the ambient tenant.
    /// The filtered unique index guarantees at most one.
    /// </summary>
    Task<Incident?> GetOpenByCorrelationKeyAsync(
        string correlationKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Incidents whose response or resolution deadline has passed without the corresponding
    /// step being taken, as of <paramref name="now"/> — the candidates the SLA sweep examines
    /// (plan, UC-12).
    /// </summary>
    Task<IReadOnlyList<Incident>> GetSlaOverdueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
