using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// An immutable entry on an incident's timeline (ERD §1, "إضافة فقط").
/// </summary>
/// <remarks>
/// Implements <see cref="IAppendOnly"/>: the persistence layer rejects any update or
/// delete, so the timeline cannot be rewritten after the fact.
/// </remarks>
public sealed class IncidentEvent : TenantOwnedEntity, IAppendOnly
{
    private IncidentEvent(
        Guid tenantId,
        Guid incidentId,
        IncidentEventType type,
        string? payload,
        Guid? apiKeyId,
        DateTimeOffset occurredAt)
    {
        TenantId = tenantId;
        IncidentId = incidentId;
        Type = type;
        Payload = payload;
        ApiKeyId = apiKeyId;
        OccurredAt = occurredAt;
    }

    public Guid IncidentId { get; private set; }

    public IncidentEventType Type { get; private set; }

    /// <summary>Raw event body, or null when the event carries no payload.</summary>
    public string? Payload { get; private set; }

    /// <summary>
    /// API key that ingested the event, or null when a user produced it (ERD §3, "حوادث").
    /// </summary>
    public Guid? ApiKeyId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static IncidentEvent Create(
        Guid tenantId,
        Guid incidentId,
        IncidentEventType type,
        DateTimeOffset? occurredAt = null,
        string? payload = null,
        Guid? apiKeyId = null)
    {
        return new IncidentEvent(
            tenantId,
            incidentId,
            type,
            payload?.Trim(),
            apiKeyId,
            occurredAt ?? DateTimeOffset.UtcNow);
    }
}
