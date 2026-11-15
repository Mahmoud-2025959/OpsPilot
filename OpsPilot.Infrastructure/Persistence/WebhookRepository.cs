using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class WebhookRepository : IWebhookRepository
{
    private readonly AppDbContext _context;

    public WebhookRepository(AppDbContext context) => _context = context;

    public async Task<Guid> AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        _context.WebhookEndpoints.Add(endpoint);
        await _context.SaveChangesAsync(cancellationToken);
        return endpoint.Id;
    }

    public Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.WebhookEndpoints.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        _context.WebhookEndpoints.Update(endpoint);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        // Retired, not erased: the calls already made still point at the endpoint they were
        // made to, and the row has to stay for that to mean anything.
        endpoint.SoftDelete();

        // The calls still waiting for this endpoint are parked rather than left waiting for a
        // destination that will never answer them again.
        var pending = await _context.WebhookDeliveries
            .Where(d => d.EndpointId == endpoint.Id && d.Status == WebhookDeliveryStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var delivery in pending)
        {
            delivery.MarkFailed("The endpoint was retired before this call was made.");
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<WebhookEndpoint> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient tenant.
        var query = _context.WebhookEndpoints.AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> UrlExistsAsync(
        string url,
        Guid? excludingId = null,
        CancellationToken cancellationToken = default)
    {
        var trimmed = url.Trim();

        return _context.WebhookEndpoints.AnyAsync(
            e => e.Url == trimmed && (excludingId == null || e.Id != excludingId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<(WebhookDelivery Delivery, WebhookEndpoint Endpoint)>>
        GetPendingDeliveriesAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        // The endpoint comes along with the delivery: a call cannot be made without knowing
        // where to make it, and one query is cheaper than two.
        var rows = await (
            from delivery in _context.WebhookDeliveries
            join endpoint in _context.WebhookEndpoints on delivery.EndpointId equals endpoint.Id
            where delivery.Status == WebhookDeliveryStatus.Pending
            orderby delivery.CreatedAt
            select new { Delivery = delivery, Endpoint = endpoint })
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.Delivery, row.Endpoint)).ToList();
    }

    public async Task SaveDeliveriesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<WebhookDelivery> Items, int TotalCount)> GetDeliveriesPagedAsync(
        Guid? endpointId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.WebhookDeliveries.AsQueryable();

        if (endpointId is { } id)
        {
            query = query.Where(d => d.EndpointId == id);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
