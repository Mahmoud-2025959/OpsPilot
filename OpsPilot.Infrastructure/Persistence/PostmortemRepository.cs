using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class PostmortemRepository : IPostmortemRepository
{
    private readonly AppDbContext _context;

    public PostmortemRepository(AppDbContext context) => _context = context;

    public async Task<Guid> AddAsync(Postmortem postmortem, CancellationToken cancellationToken = default)
    {
        _context.Postmortems.Add(postmortem);
        await _context.SaveChangesAsync(cancellationToken);
        return postmortem.Id;
    }

    public async Task<Postmortem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Postmortems.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Postmortem?> GetByIncidentAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Postmortems
            .SingleOrDefaultAsync(p => p.IncidentId == incidentId, cancellationToken);
    }

    public async Task UpdateAsync(Postmortem postmortem, CancellationToken cancellationToken = default)
    {
        _context.Postmortems.Update(postmortem);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Postmortem> Items, int TotalCount)> GetPagedAsync(
        PostmortemStatus? status,
        Guid? incidentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient company.
        var query = _context.Postmortems.AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (incidentId.HasValue && incidentId.Value != Guid.Empty)
        {
            query = query.Where(p => p.IncidentId == incidentId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
