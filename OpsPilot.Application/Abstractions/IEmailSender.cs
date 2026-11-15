namespace OpsPilot.Application.Abstractions;

/// <summary>Puts one email on the wire (plan, notifications group: the email channel's delivery step).</summary>
/// <remarks>
/// The sweep that drains the notification queue talks to this interface, never to a mail
/// library, for the same reason the outbox talks to <see cref="IOutboxPublisher"/>: the queue's
/// retry rules must be testable without a mail server, and the transport must be swappable
/// without touching them.
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Hands the message to the transport. Throws when it cannot be delivered; whether that
    /// failure deserves another attempt is the caller's decision, not the transport's.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
