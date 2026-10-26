using OpsPilot.Application.Common.Messages;

namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Applies one kind of integration event that arrived over the broker (plan, UC-06 step 7).
/// </summary>
/// <remarks>
/// The registry is keyed by <see cref="Type"/>: the consumer looks for the handler that
/// answers the event it received, and an event nobody answers is recorded and acknowledged
/// rather than retried forever. Adding a new kind of event is therefore a matter of writing a
/// handler and registering it — nothing in the consumer has to change.
/// </remarks>
public interface IIntegrationEventHandler
{
    /// <summary>The event type this handler answers to.</summary>
    string Type { get; }

    /// <summary>Applies the event.</summary>
    Task HandleAsync(IntegrationEventMessage message, CancellationToken cancellationToken = default);
}
