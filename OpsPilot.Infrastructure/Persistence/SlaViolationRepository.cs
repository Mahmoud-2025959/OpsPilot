using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Persists SLA breaches (plan, UC-12).
/// </summary>
public sealed class SlaViolationRepository : ISlaViolationRepository
{
    private readonly AppDbContext _context;

    public SlaViolationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<SlaViolation>> GetForIncidentsAsync(
        IReadOnlyCollection<Guid> incidentIds,
        CancellationToken cancellationToken = default)
    {
        if (incidentIds.Count == 0)
        {
            return [];
        }

        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient tenant.
        return await _context.SlaViolations
            .AsNoTracking()
            .Where(violation => incidentIds.Contains(violation.IncidentId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        SlaViolation violation,
        IncidentEvent timelineEntry,
        CancellationToken cancellationToken = default)
    {
        // One save for both, so a violation is never visible without its timeline entry. The
        // unique index on (IncidentId, SlaType) is the other half of this: a second sweep that
        // races this one cannot get a second row in.
        _context.SlaViolations.Add(violation);
        _context.IncidentEvents.Add(timelineEntry);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
