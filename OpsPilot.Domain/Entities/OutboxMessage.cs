using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// Transactional outbox row: written in the same transaction as the business change and
/// published to the broker afterwards (ERD §3, "جداول البنية التحتية").
/// </summary>
/// <remarks>
/// <see cref="TenantId"/> is carried for traceability only and deliberately has no foreign
/// key, so publishing can never be blocked by tenant state. This table is global rather
/// than tenant-scoped in the query-filter sense: the dispatcher scans it across tenants.
/// </remarks>
public sealed class OutboxMessage : TenantOwnedEntity
{
    private OutboxMessage(string type, string payload)
    {
        Type = type;
        Payload = payload;
    }

    public string Type { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public OutboxMessageState State { get; private set; } = OutboxMessageState.Pending;

    public int Attempts { get; private set; }

    public string? Error { get; private set; }

    public static OutboxMessage Create(string type, string payload, Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        return new OutboxMessage(type.Trim(), payload)
        {
            TenantId = tenantId ?? Guid.Empty
        };
    }

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt = at;
        State = OutboxMessageState.Processed;
        Error = null;
    }

    /// <summary>
    /// Notes a publish that failed but may still be retried, leaving the row in the queue.
    /// </summary>
    /// <remarks>
    /// Kept separate from <see cref="MarkFailed"/> because "tried once and it did not go" and
    /// "tried enough" deserve different answers: the first stays in the queue, the second waits
    /// for someone to look at it.
    /// </remarks>
    public void RecordFailedAttempt(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        Attempts++;
        Error = error.Trim();
    }

    public void MarkFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        Attempts++;
        Error = error.Trim();
        State = OutboxMessageState.Failed;
    }
}
