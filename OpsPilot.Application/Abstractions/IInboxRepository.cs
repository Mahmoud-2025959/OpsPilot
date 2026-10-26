namespace OpsPilot.Application.Abstractions;

/// <summary>
/// The ledger of consumed messages (ERD §3, "جداول البنية التحتية").
/// </summary>
/// <remarks>
/// The broker promises at-least-once delivery, not exactly-once. Recording every message a
/// consumer has taken lets a redelivery be recognised and skipped, so the work behind a
/// message happens once even though the broker may hand it over twice.
/// </remarks>
public interface IInboxRepository
{
    /// <summary>
    /// Claims a message for handling.
    /// </summary>
    /// <remarks>
    /// The claim is written before the work is done, which is what makes the redelivery
    /// recognisable: the row is there, so the second delivery is skipped.
    /// </remarks>
    /// <returns>
    /// True when this call is the one that claimed the message, false when it had already been
    /// claimed and the caller should skip the work.
    /// </returns>
    Task<bool> TryBeginAsync(string messageId, string consumer, CancellationToken cancellationToken = default);

    /// <summary>Records that the message was handled.</summary>
    Task MarkProcessedAsync(
        string messageId,
        string consumer,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}
