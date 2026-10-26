using OpsPilot.Application.Abstractions;
using OpsPilot.Contracts.Notifications;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Captures what would have gone onto the realtime channel, for tests that drive handlers
/// directly instead of over a socket.
/// </summary>
/// <remarks>
/// Recording rather than discarding lets a test assert that the push happened — who was told,
/// about which incident — without standing up a hub connection and waiting on it.
/// </remarks>
internal sealed class RecordingIncidentNotifier : IIncidentNotifier
{
    public List<(Guid TenantId, Guid IncidentId)> IncidentUpdates { get; } = [];

    public List<(Guid TenantId, Guid UserId, NotificationDto Notification)> Notifications { get; } = [];

    public Task IncidentUpdatedAsync(
        Guid tenantId,
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        IncidentUpdates.Add((tenantId, incidentId));
        return Task.CompletedTask;
    }

    public Task NotificationCreatedAsync(
        Guid tenantId,
        Guid userId,
        NotificationDto notification,
        CancellationToken cancellationToken = default)
    {
        Notifications.Add((tenantId, userId, notification));
        return Task.CompletedTask;
    }
}
