using Microsoft.AspNetCore.SignalR;
using OpsPilot.Application.Abstractions;
using OpsPilot.API.Hubs;
using OpsPilot.Contracts.Notifications;

namespace OpsPilot.API.Realtime;

/// <summary>
/// Delivers the application's live updates over SignalR (plan, FR-20 and UC-15).
/// </summary>
/// <remarks>
/// The groups are named through <see cref="HubProtocol"/>, the same source the hub joins them
/// from, which is the entire contract between "who is listening" and "who gets a message": the
/// notifier names a group and never a connection, so a user with three browsers open needs no
/// extra bookkeeping. Transport failures are logged and swallowed — see
/// <see cref="IIncidentNotifier"/> — so a hub hiccup cannot roll back an assignment that
/// already succeeded.
/// </remarks>
public sealed class IncidentsHubNotifier : IIncidentNotifier
{
    private readonly IHubContext<IncidentsHub> _hub;
    private readonly ILogger<IncidentsHubNotifier> _logger;

    public IncidentsHubNotifier(
        IHubContext<IncidentsHub> hub,
        ILogger<IncidentsHubNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task IncidentUpdatedAsync(
        Guid tenantId,
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients
                .Group(HubProtocol.TenantGroup(tenantId))
                .SendAsync(
                    HubProtocol.IncidentUpdated,
                    new { incidentId },
                    cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not push an incident update for incident {IncidentId}.",
                incidentId);
        }
    }

    public async Task NotificationCreatedAsync(
        Guid tenantId,
        Guid userId,
        NotificationDto notification,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hub.Clients
                .Group(HubProtocol.UserGroup(userId))
                .SendAsync(
                    HubProtocol.NotificationCreated,
                    notification,
                    cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not push a notification for user {UserId}.",
                userId);
        }
    }
}
