using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// One webhook call waiting to be made (ERD §3, "السلا والإشعارات" — the webhook channel).
/// </summary>
/// <remarks>
/// <para>
/// A delivery is staged in the same transaction as the event it carries, so a webhook can
/// never be told about something the system did not store, and something the system stored
/// can never be missing from the list of webhooks that should have heard about it.
/// </para>
/// <para>
/// The row outlives the call. A delivery that failed keeps its reason and its attempt count,
/// so an operator can see that an endpoint stopped answering instead of finding out from the
/// silence.
/// </para>
/// </remarks>
public sealed class WebhookDelivery : TenantOwnedEntity
{
    private WebhookDelivery(Guid endpointId, string eventType, string payload)
    {
        EndpointId = endpointId;
        EventType = eventType;
        Payload = payload;
    }

    /// <summary>The endpoint the call is made to.</summary>
    public Guid EndpointId { get; private set; }

    /// <summary>The kind of event being announced, e.g. <c>IncidentCreated</c>.</summary>
    public string EventType { get; private set; }

    /// <summary>The body of the call, exactly as it goes on the wire.</summary>
    public string Payload { get; private set; }

    public WebhookDeliveryStatus Status { get; private set; } = WebhookDeliveryStatus.Pending;

    /// <summary>How many times a delivery was attempted before this row settled.</summary>
    public int Attempts { get; private set; }

    /// <summary>Why the last attempt failed, kept so a queued row is never a silent one.</summary>
    public string? LastError { get; private set; }

    /// <summary>When the endpoint accepted the call.</summary>
    public DateTimeOffset? DeliveredAt { get; private set; }

    public static WebhookDelivery Create(
        Guid tenantId,
        Guid endpointId,
        string eventType,
        string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new WebhookDelivery(endpointId, eventType.Trim(), payload)
        {
            TenantId = tenantId
        };
    }

    public void MarkDelivered(DateTimeOffset at)
    {
        if (Status is WebhookDeliveryStatus.Delivered)
        {
            return;
        }

        Status = WebhookDeliveryStatus.Delivered;
        DeliveredAt = at;

        // The attempt count stays — it shows the delivery was not smooth — but the error is
        // cleared because it no longer describes this row's state.
        LastError = null;
    }

    /// <summary>Notes a failed attempt and keeps the row queued for the next pass.</summary>
    public void RecordFailedAttempt(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        Attempts++;
        LastError = error.Trim();
    }

    /// <summary>Parks the row: this endpoint will not be tried again without a human's say-so.</summary>
    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        if (Status is WebhookDeliveryStatus.Delivered)
        {
            throw new InvalidStateTransitionException("A delivered webhook cannot be marked as failed.");
        }

        Attempts++;
        LastError = error.Trim();
        Status = WebhookDeliveryStatus.Failed;
    }
}
