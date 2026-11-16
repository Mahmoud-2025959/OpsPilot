using System.Text.Json;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Messages;

/// <summary>
/// Builds the integration messages that announce an incident change (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// Every path that writes a timeline entry — ingestion, manual creation, state changes,
/// comments and assignment — announces it the same way. Keeping the shape in one place is what
/// makes a webhook and a broker message say the same thing about the same event, whichever door
/// the change came through.
/// </remarks>
public static class IncidentAnnouncements
{
    /// <summary>
    /// The message for an incident's first timeline entry. The type is
    /// <c>IncidentCreated</c> whether the incident was opened by ingestion or by hand.
    /// </summary>
    public static OutboxMessage Created(IncidentEvent entry, Incident incident) =>
        Announce(IntegrationEventTypes.IncidentCreated, entry, incident);

    /// <summary>
    /// The message for a later entry on an existing incident's timeline.
    /// </summary>
    public static OutboxMessage Added(IncidentEvent entry, Incident incident) =>
        Announce(IntegrationEventTypes.IncidentEventAdded, entry, incident);

    /// <summary>
    /// Carries the two ids a consumer needs plus the timeline kind, so the receiver can tell
    /// what happened without reading the timeline back. The body of the event stays in the
    /// timeline rather than being copied here.
    /// </summary>
    private static OutboxMessage Announce(string type, IncidentEvent entry, Incident incident) =>
        OutboxMessage.Create(
            type,
            JsonSerializer.Serialize(new
            {
                eventId = entry.Id,
                incidentId = incident.Id,
                eventType = entry.Type.ToString()
            }),
            incident.TenantId);
}
