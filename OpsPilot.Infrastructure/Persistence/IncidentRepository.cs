using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly AppDbContext _context;
    private readonly IIncidentNotifier _notifier;

    public IncidentRepository(AppDbContext context, IIncidentNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    public async Task<Guid> AddAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        _context.Incidents.Add(incident);
        await _context.SaveChangesAsync(cancellationToken);
        return incident.Id;
    }

    public async Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Incidents.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Incident incident, CancellationToken cancellationToken = default)
    {
        _context.Incidents.Update(incident);
        await _context.SaveChangesAsync(cancellationToken);

        // Every change to a stored incident is announced from here -- a status move, a
        // reassignment, an edit through the PUT endpoint -- which keeps the fan-out in one
        // place instead of one call in every handler that happens to touch an incident. The
        // tenant is taken from the entity, not the ambient scope, because this call also
        // serves background sweeps with no signed-in user. Delivery is best-effort, so a hub
        // problem cannot fail the change that triggered it.
        await _notifier.IncidentUpdatedAsync(incident.TenantId, incident.Id, cancellationToken);
    }

    public async Task<(IReadOnlyList<Incident> Items, int TotalCount)> GetPagedAsync(
        IncidentStatus? status,
        Severity? severity,
        Guid? serviceId,
        Guid? assignedTo,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient tenant.
        var query = _context.Incidents.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        if (severity.HasValue)
        {
            query = query.Where(i => i.Severity == severity.Value);
        }

        if (serviceId.HasValue && serviceId.Value != Guid.Empty)
        {
            query = query.Where(i => i.ServiceId == serviceId.Value);
        }

        if (assignedTo.HasValue && assignedTo.Value != Guid.Empty)
        {
            query = query.Where(i => i.AssignedTo == assignedTo.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(i => i.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(i => i.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddCommentAsync(IncidentComment comment, CancellationToken cancellationToken = default)
    {
        _context.IncidentComments.Add(comment);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncidentComment>> GetCommentsAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        return await _context.IncidentComments
            .Where(c => c.IncidentId == incidentId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddEventAsync(IncidentEvent incidentEvent, CancellationToken cancellationToken = default)
    {
        _context.IncidentEvents.Add(incidentEvent);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddEventAsync(
        IncidentEvent incidentEvent,
        OutboxMessage integrationMessage,
        CancellationToken cancellationToken = default)
    {
        // Both rows, one transaction: the timeline entry and the message that announces it are
        // one fact, so the outbox can never reference an entry that was not stored (plan, UC-06
        // step 7).
        _context.IncidentEvents.Add(incidentEvent);
        _context.OutboxMessages.Add(integrationMessage);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IncidentEvent>> GetEventsAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        return await _context.IncidentEvents
            .Where(e => e.IncidentId == incidentId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Incident?> GetOpenByCorrelationKeyAsync(
        string correlationKey,
        CancellationToken cancellationToken = default)
    {
        // "Open" is everything that is neither Resolved nor Closed -- exactly the predicate of
        // the filtered unique index, so the database guarantees at most one match. Tenant scoping
        // comes from the global query filter.
        return _context.Incidents.FirstOrDefaultAsync(
            i => i.CorrelationKey == correlationKey
                && i.Status != IncidentStatus.Resolved
                && i.Status != IncidentStatus.Closed,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Incident>> GetSlaOverdueAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // Both deadlines are evaluated in the database so the sweep only pulls the incidents it
        // can actually act on, rather than the tenant's whole incident history. An incident with
        // no policy attached has no deadlines and is therefore never a candidate. Read-only: the
        // sweep records breaches against incidents; it does not change the incidents themselves.
        return await _context.Incidents
            .AsNoTracking()
            .Where(i =>
                (i.ResponseDueAt != null && i.AcknowledgedAt == null && i.ResponseDueAt < now)
                || (i.ResolutionDueAt != null && i.ResolvedAt == null && i.ResolutionDueAt < now))
            .ToListAsync(cancellationToken);
    }
}