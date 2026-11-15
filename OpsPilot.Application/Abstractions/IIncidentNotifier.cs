using OpsPilot.Contracts.Notifications;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Pushes live updates to whoever is watching (plan, FR-20 and UC-15).
/// </summary>
/// <remarks>
/// Delivery is best-effort: a browser that disconnected mid-flight, or a hub that is slow to
/// answer, must never be able to fail the business operation that happened to trigger a push.
/// Implementations swallow and log their own transport failures, so callers do not wrap these
/// calls in try/catch — and a missing realtime channel degrades to "the client refetches",
/// never to a lost assignment.
/// </remarks>
public interface IIncidentNotifier
{
    /// <summary>
    /// Announces that an incident changed, to everyone signed in to the tenant. The payload is
    /// the id alone on purpose: clients refetch the incident, so one push serves every kind of
    /// change without a payload that goes stale the moment it is written.
    /// </summary>
    Task IncidentUpdatedAsync(
        Guid tenantId,
        Guid incidentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers one queued notification to the recipient's own live sessions.
    /// </summary>
    Task NotificationCreatedAsync(
        Guid tenantId,
        Guid userId,
        NotificationDto notification,
        CancellationToken cancellationToken = default);
}
