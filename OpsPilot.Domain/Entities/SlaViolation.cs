using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A record that an incident missed one of its SLA deadlines.
/// </summary>
/// <remarks>
/// The unique index on <c>(IncidentId, SlaType)</c> is what makes the detection job
/// idempotent: re-running it cannot produce a second breach row or a second notification.
/// </remarks>
public sealed class SlaViolation : TenantOwnedEntity
{
    private SlaViolation(
        Guid tenantId,
        Guid incidentId,
        SlaType slaType,
        DateTimeOffset dueAt,
        DateTimeOffset detectedAt)
    {
        TenantId = tenantId;
        IncidentId = incidentId;
        SlaType = slaType;
        DueAt = dueAt;
        DetectedAt = detectedAt;
    }

    public Guid IncidentId { get; private set; }

    /// <summary>Which deadline was missed. Persisted as the <c>Type</c> column (ERD §2).</summary>
    public SlaType SlaType { get; private set; }

    public DateTimeOffset DueAt { get; private set; }

    /// <summary>When the breach was observed (ERD's <c>DetectedAt</c>).</summary>
    public DateTimeOffset DetectedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsOpen => ResolvedAt is null;

    public static SlaViolation Create(
        Guid tenantId,
        Guid incidentId,
        SlaType slaType,
        DateTimeOffset dueAt,
        DateTimeOffset detectedAt)
    {
        if (detectedAt < dueAt)
        {
            throw new DomainException("SLA violation cannot be detected before the due time.");
        }

        return new SlaViolation(tenantId, incidentId, slaType, dueAt, detectedAt);
    }

    public void MarkResolved(DateTimeOffset at)
    {
        ResolvedAt ??= at;
    }
}
