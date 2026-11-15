using Microsoft.EntityFrameworkCore;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Infrastructure.Persistence;

/// <summary>
/// Fans a domain event out to its recipients, honouring each user's channel preferences
/// (plan, FR-16 and UC-13).
/// </summary>
/// <remarks>
/// <para>
/// An absent preference row means enabled: opting out is the exception that gets stored,
/// not the opting in. That keeps a brand-new user reachable by default and means adding a
/// channel later cannot silently mute everyone who never touched their settings.
/// </para>
/// <para>
/// Like the audit writer, the tenant is read from the ambient scope rather than taken as a
/// parameter — every caller acts inside a unit of work that has already resolved to one
/// company, and a notification filed under the wrong tenant would be invisible to the people
/// it was meant for.
/// </para>
/// </remarks>
public sealed class NotificationService : INotificationService
{
    private static readonly NotificationChannel[] Channels =
    [
        NotificationChannel.Email,
        NotificationChannel.InApp
    ];

    private readonly AppDbContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly IIncidentNotifier _notifier;

    public NotificationService(
        AppDbContext context,
        ICurrentTenant currentTenant,
        IIncidentNotifier notifier)
    {
        _context = context;
        _currentTenant = currentTenant;
        _notifier = notifier;
    }

    public async Task NotifyAsync(
        IReadOnlyCollection<Guid> userIds,
        NotificationType type,
        Severity severity,
        string title,
        string body,
        Guid? incidentId = null,
        CancellationToken cancellationToken = default)
    {
        // Fail loudly rather than filing notifications under an unresolved tenant: the query
        // filter would hide the rows from everyone, turning a mistake into silently lost work.
        if (!_currentTenant.IsResolved)
        {
            throw new InvalidOperationException(
                $"Cannot queue a '{type}' notification before a tenant has been resolved.");
        }

        var recipients = userIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        var muted = await GetMutedChannelsAsync(recipients, severity, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var delivered = new List<Notification>();

        foreach (var userId in recipients)
        {
            foreach (var channel in Channels)
            {
                if (muted.Contains((userId, channel)))
                {
                    continue;
                }

                var notification = Notification.Create(
                    _currentTenant.TenantId,
                    userId,
                    channel,
                    type,
                    title,
                    body,
                    incidentId);

                // The in-app channel has no delivery step — the row is the message, so it is
                // sent the moment it is written. Email keeps waiting for a mailer that can
                // actually put it on the wire, and therefore stays out of the realtime push:
                // announcing a mail nobody has sent yet would be telling the user something
                // untrue.
                if (channel == NotificationChannel.InApp)
                {
                    notification.MarkSent(now);
                    delivered.Add(notification);
                }

                await _context.Notifications.AddAsync(notification, cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Pushed after the write, never before: a client that reacts by refetching its inbox
        // must find the row already there. Delivery itself is best-effort -- see
        // IIncidentNotifier -- so a failed push cannot fail the assignment that caused it.
        foreach (var notification in delivered)
        {
            await _notifier.NotificationCreatedAsync(
                notification.TenantId,
                notification.UserId,
                notification.ToDto(),
                cancellationToken);
        }
    }

    /// <summary>
    /// Only the opt-outs need reading: a user with no row for the severity is reachable over
    /// every channel, so the common case costs one small query and no per-user round trips.
    /// </summary>
    private async Task<HashSet<(Guid UserId, NotificationChannel Channel)>> GetMutedChannelsAsync(
        IReadOnlyList<Guid> userIds,
        Severity severity,
        CancellationToken cancellationToken)
    {
        var disabled = await _context.NotificationPreferences
            .Where(p => userIds.Contains(p.UserId)
                && p.Severity == severity
                && !p.IsEnabled)
            .ToListAsync(cancellationToken);

        return disabled
            .Select(row => (row.UserId, row.Channel))
            .ToHashSet();
    }
}
