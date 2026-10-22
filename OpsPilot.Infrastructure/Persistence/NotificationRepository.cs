using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedForUserAsync(
        Guid userId,
        bool? isRead,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Tenant scoping is deliberately absent: the global query filter has already narrowed
        // this set to the ambient tenant, which is also what makes the TenantId+UserId index
        // serve this query.
        var query = _context.Notifications.Where(n => n.UserId == userId);

        if (isRead.HasValue)
        {
            var read = isRead.Value;
            query = query.Where(n => read ? n.ReadAt != null : n.ReadAt == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Sorted by the enum's numeric value, not by SQL: the columns hold names, so the
        // database would order them alphabetically (Critical, High, Low, Medium).
        var rows = await _context.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(p => p.Severity)
            .ThenBy(p => p.Channel)
            .ToList();
    }

    public async Task SavePreferencesAsync(
        IReadOnlyList<NotificationPreference> added,
        CancellationToken cancellationToken = default)
    {
        await _context.NotificationPreferences.AddRangeAsync(added, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<(Notification Item, string? RecipientEmail)>> GetPendingEmailsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // Oldest first: a burst of today's alerts must not push yesterday's email further back
        // in the queue. Channel and status narrow the scan on top of the tenant filter the
        // context has already applied.
        var batch = await _context.Notifications
            .Where(n => n.Channel == NotificationChannel.Email
                && n.Status == NotificationStatus.Pending)
            .OrderBy(n => n.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            return [];
        }

        // One lookup for the whole batch rather than one query per row. Recipients the filters
        // hide - a soft-deleted account, for instance - simply miss here, and the caller treats
        // the gap as an address that no longer exists.
        var userIds = batch.Select(n => n.UserId).Distinct().ToList();
        var addresses = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken);

        var items = new List<(Notification Item, string? RecipientEmail)>(batch.Count);

        foreach (var notification in batch)
        {
            items.Add((notification, addresses.GetValueOrDefault(notification.UserId)));
        }

        return items;
    }

    public Task SaveDeliveriesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
