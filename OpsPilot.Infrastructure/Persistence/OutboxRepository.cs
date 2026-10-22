using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// The transactional outbox store (plan, UC-06 step 7).
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _context;

    public OutboxRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // The one read that has to opt out of the tenant filter: the queue is global, and the
        // dispatcher has no ambient company to be narrowed to. Oldest first, so a message cannot
        // be overtaken by the ones queued behind it.
        return await _context.OutboxMessages
            .IgnoreQueryFilters()
            .Where(message => message.State == OutboxMessageState.Pending)
            .OrderBy(message => message.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
