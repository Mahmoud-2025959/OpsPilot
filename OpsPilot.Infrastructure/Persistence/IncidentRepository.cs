using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly AppDbContext _context;

    public IncidentRepository(AppDbContext context)
    {
        _context = context;
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
    }

    public async Task<(IReadOnlyList<Incident> Items, int TotalCount)> GetPagedAsync(
        Guid? tenantId,
        IncidentStatus? status,
        Severity? severity,
        Guid? serviceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Incidents.AsQueryable();

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(i => i.TenantId == tenantId.Value);
        }

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

    public async Task<IReadOnlyList<IncidentEvent>> GetEventsAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        return await _context.IncidentEvents
            .Where(e => e.IncidentId == incidentId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
    }
}