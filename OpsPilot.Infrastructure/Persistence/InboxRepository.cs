using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// The consumed-message ledger (ERD §3, "جداول البنية التحتية").
/// </summary>
/// <remarks>
/// The table is global rather than tenant-scoped: it is keyed by the broker's message id, which
/// is unique across every company, and it carries no tenant of its own. The company that owns a
/// message is known to the consumer that reads it, not to the ledger.
/// </remarks>
public sealed class InboxRepository : IInboxRepository
{
    private readonly AppDbContext _context;

    public InboxRepository(AppDbContext context) => _context = context;

    public async Task<bool> TryBeginAsync(
        string messageId,
        string consumer,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        var taken = await _context.InboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.MessageId == messageId, cancellationToken);

        if (taken)
        {
            return false;
        }

        _context.InboxMessages.Add(InboxMessage.Create(messageId, consumer));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another consumer claimed the same message between the check above and the insert.
            // The primary key on MessageId is the real guarantee; the check only saves the
            // common case from having to raise an error at all.
            _context.ChangeTracker.Clear();
            return false;
        }

        return true;
    }

    public async Task MarkProcessedAsync(
        string messageId,
        string consumer,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var message = await _context.InboxMessages
            .FirstOrDefaultAsync(m => m.MessageId == messageId, cancellationToken);

        message?.MarkProcessed(at);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
