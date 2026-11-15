namespace OpsPilot.Application.Abstractions;

/// <summary>
/// One webhook call, addressed and signed and ready to go on the wire.
/// </summary>
/// <param name="Url">The endpoint that receives the call.</param>
/// <param name="EventType">The kind of event being announced.</param>
/// <param name="DeliveryId">The id of the delivery, for the receiver to quote back.</param>
/// <param name="Body">The exact bytes of the body, so the signature covers what is sent.</param>
/// <param name="Secret">The shared signing secret.</param>
/// <param name="Timestamp">When the call was made; part of what is signed.</param>
public sealed record WebhookDeliveryRequest(
    string Url,
    string EventType,
    string DeliveryId,
    string Body,
    string Secret,
    DateTimeOffset Timestamp);

/// <summary>
/// Puts one webhook call on the wire (plan, notifications group: the webhook channel's
/// delivery step).
/// </summary>
/// <remarks>
/// The dispatcher that drains the queue talks to this interface, never to an HTTP client, for
/// the same reason the outbox talks to <see cref="IOutboxPublisher"/>: the queue's retry
/// rules must be testable without a server on the other end, and the transport must be
/// swappable without touching them.
/// </remarks>
public interface IWebhookSender
{
    /// <summary>
    /// Hands the call to the transport. Throws when it cannot be delivered; whether that
    /// failure deserves another attempt is the caller's decision, not the transport's.
    /// </summary>
    Task SendAsync(WebhookDeliveryRequest request, CancellationToken cancellationToken = default);
}
