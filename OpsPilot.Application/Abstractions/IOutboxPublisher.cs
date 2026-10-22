namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Hands a staged integration message to the outside world (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// The transport is deliberately not named here. Today's implementation only logs, and swapping
/// it for a broker replaces one registration — neither the handlers that fill the outbox nor the
/// dispatcher that drains it know which one is wired.
/// </remarks>
public interface IOutboxPublisher
{
    /// <summary>
    /// Publishes one message. Throwing is how a delivery failure is reported: the dispatcher
    /// keeps the row and tries again later.
    /// </summary>
    Task PublishAsync(string type, string payload, CancellationToken cancellationToken = default);
}
