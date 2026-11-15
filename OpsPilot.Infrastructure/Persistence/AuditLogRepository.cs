using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetPagedAsync(
        string? action,
        string? entityType,
        string? entityId,
        Guid? actorUserId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient tenant, which is also what makes the TenantId+CreatedAt index
        // serve this query.
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(a => a.EntityId == entityId);
        }

        if (actorUserId.HasValue && actorUserId.Value != Guid.Empty)
        {
            query = query.Where(a => a.ActorUserId == actorUserId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= to.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
