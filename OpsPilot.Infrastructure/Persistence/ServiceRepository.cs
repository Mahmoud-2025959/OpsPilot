using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class ServiceRepository : IServiceRepository
{
    private readonly AppDbContext _context;

    public ServiceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> AddAsync(Service service, CancellationToken cancellationToken = default)
    {
        await _context.Services.AddAsync(service, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return service.Id;
    }

    public Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Services.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(Service service, CancellationToken cancellationToken = default)
    {
        _context.Services.Update(service);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Service> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient tenant.
        var query = _context.Services.AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Environment)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> NameExistsAsync(
        string name,
        EnvironmentType environment,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();

        return _context.Services.AnyAsync(
            s => s.Name == trimmed
                && s.Environment == environment
                && (excludingId == null || s.Id != excludingId),
            cancellationToken);
    }

    public Task<Service?> GetByNameAsync(
        string name,
        EnvironmentType environment,
        CancellationToken cancellationToken = default)
    {
        // The unique index on (TenantId, Name, Environment) makes this at most one row; the
        // tenant filter is the ambient one, so the lookup cannot reach another company.
        var trimmed = name.Trim();

        return _context.Services.FirstOrDefaultAsync(
            s => s.Name == trimmed && s.Environment == environment,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> GetServiceManagersAsync(
        IReadOnlyCollection<Guid> serviceIds,
        CancellationToken cancellationToken = default)
    {
        if (serviceIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        // One query for the whole batch. Only services that have an owner team with leads
        // appear in the result; the rest are absent rather than present with an empty list,
        // which keeps the caller's loop free of "does this key exist" checks.
        var rows = await (
                from service in _context.Services
                join team in _context.Teams on service.OwnerTeamId equals team.Id
                join member in _context.TeamMembers on team.Id equals member.TeamId
                where serviceIds.Contains(service.Id)
                    && member.Role == TeamRole.Lead
                select new { service.Id, member.UserId })
            .ToListAsync(cancellationToken);

        var managers = new Dictionary<Guid, IReadOnlyList<Guid>>();

        foreach (var byService in rows.GroupBy(row => row.Id))
        {
            managers[byService.Key] = byService
                .Select(row => row.UserId)
                .Distinct()
                .ToList();
        }

        return managers;
    }
}
