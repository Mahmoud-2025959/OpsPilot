using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Abstractions;

public interface IIncidentRepository
{
    Task<Guid> AddAsync(Incident incident, CancellationToken cancellationToken = default);

    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Incident> Items, int TotalCount)> GetPagedAsync(
        Guid? tenantId,
        IncidentStatus? status,
        Severity? severity,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddCommentAsync(IncidentComment comment, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentComment>> GetCommentsAsync(Guid incidentId, CancellationToken cancellationToken = default);

    Task AddEventAsync(IncidentEvent incidentEvent, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentEvent>> GetEventsAsync(Guid incidentId, CancellationToken cancellationToken = default);
}