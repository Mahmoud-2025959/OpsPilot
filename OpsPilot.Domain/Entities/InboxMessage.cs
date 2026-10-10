namespace OpsPilot.Domain.Entities;

/// <summary>
/// Idempotency ledger for consumed messages (ERD §3, "جداول البنية التحتية").
/// </summary>
/// <remarks>
/// The broker guarantees at-least-once delivery, not exactly-once. Recording every
/// processed <see cref="MessageId"/> per <see cref="Consumer"/> lets a consumer skip a
/// redelivery it has already handled. Intentionally has no <see cref="BaseEntity"/>:
/// it is infrastructure plumbing, carries no tenant, and is keyed by a string.
/// </remarks>
public sealed class InboxMessage
{
    private InboxMessage(string messageId, string consumer)
    {
        MessageId = messageId;
        Consumer = consumer;
    }

    /// <summary>Broker-assigned identifier of the message (primary key).</summary>
    public string MessageId { get; private set; }

    /// <summary>Name of the consumer that handled the message.</summary>
    public string Consumer { get; private set; }

    /// <summary>When the consumer finished handling the message, or null while in flight.</summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    public static InboxMessage Create(string messageId, string consumer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        return new InboxMessage(messageId.Trim(), consumer.Trim());
    }

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAt ??= at;
    }
}
