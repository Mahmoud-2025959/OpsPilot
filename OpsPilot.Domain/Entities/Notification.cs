using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A notification queued for delivery to a user over one channel.
/// </summary>
public sealed class Notification : TenantOwnedEntity
{
    private Notification(
        Guid tenantId,
        Guid userId,
        Guid? incidentId,
        NotificationChannel channel,
        NotificationType type,
        string title,
        string body,
        DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        UserId = userId;
        IncidentId = incidentId;
        Channel = channel;
        Type = type;
        Title = title;
        Body = body;
        CreatedAt = createdAt;
        Status = NotificationStatus.Pending;
    }

    public Guid UserId { get; private set; }

    public Guid? IncidentId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; }

    public string Body { get; private set; }

    public NotificationStatus Status { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>How many times a delivery was attempted before this row settled.</summary>
    public int Attempts { get; private set; }

    /// <summary>Why the last attempt failed, kept so a queued row is never a silent one.</summary>
    public string? LastError { get; private set; }

    public bool IsRead => ReadAt is not null;

    public static Notification Create(
        Guid tenantId,
        Guid userId,
        NotificationChannel channel,
        NotificationType type,
        string title,
        string body,
        Guid? incidentId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new Notification(
            tenantId,
            userId,
            incidentId,
            channel,
            type,
            title.Trim(),
            body.Trim(),
            DateTimeOffset.UtcNow);
    }

    public void MarkSent(DateTimeOffset at)
    {
        if (Status is NotificationStatus.Sent)
        {
            return;
        }

        Status = NotificationStatus.Sent;
        SentAt = at;

        // The row keeps its attempt count - it shows the delivery was not smooth - but the
        // error is cleared because it no longer describes this row's state. Mirrors how the
        // outbox treats a message that finally went out.
        LastError = null;
    }

    /// <summary>Notes a failed delivery and keeps the row queued for the next pass.</summary>
    public void RecordFailedAttempt(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        Attempts++;
        LastError = error.Trim();
    }

    /// <summary>Parks the row: this address will not be tried again without a human's say-so.</summary>
    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        if (Status is NotificationStatus.Sent)
        {
            throw new InvalidStateTransitionException("A sent notification cannot be marked as failed.");
        }

        Attempts++;
        LastError = error.Trim();
        Status = NotificationStatus.Failed;
    }

    public void MarkRead(DateTimeOffset at)
    {
        ReadAt ??= at;
    }
}
