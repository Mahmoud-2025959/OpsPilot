using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Reads a user's notifications and applies changes to their channel preferences.
/// </summary>
/// <remarks>
/// Tenant scoping is absent from every signature on purpose: the global query filter has
/// already narrowed each set to the ambient tenant, which is also what lets the
/// <c>(TenantId, UserId, ...)</c> indexes serve these queries.
/// </remarks>
public interface INotificationRepository
{
    /// <summary>
    /// Pages one user's notifications newest-first, optionally narrowed to read or unread.
    /// </summary>
    Task<(IReadOnlyList<Notification> Items, int TotalCount)> GetPagedForUserAsync(
        Guid userId,
        bool? isRead,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationPreference>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the whole preference change as one save, so a rejected row cannot leave half
    /// the changes applied. Rows in <paramref name="added"/> are new combinations; edits made
    /// to rows handed back by <see cref="GetPreferencesAsync"/> ride along with them because
    /// the context already tracks those.
    /// </summary>
    Task SavePreferencesAsync(
        IReadOnlyList<NotificationPreference> added,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages queued email deliveries, oldest first, together with the address each one goes to.
    /// </summary>
    /// <remarks>
    /// The address is read now rather than stamped on the row when it was written: a person who
    /// corrected their email between the alert and the send gets the corrected one. A recipient
    /// that no longer resolves - a retired account, say - comes back with a null address so the
    /// dispatcher can park it deliberately instead of leaving it queued forever.
    /// </remarks>
    Task<IReadOnlyList<(Notification Item, string? RecipientEmail)>> GetPendingEmailsAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>Commits the outcomes of one delivery pass: sent rows, parked rows, attempts.</summary>
    Task SaveDeliveriesAsync(CancellationToken cancellationToken = default);
}
