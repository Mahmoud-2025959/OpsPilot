using OpsPilot.Application.Abstractions;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// A publisher that never succeeds, so a test can watch a message use up its attempts.
/// </summary>
internal sealed class FailingOutboxPublisher : IOutboxPublisher
{
    public int Attempts { get; private set; }

    public Task PublishAsync(
        string type,
        string payload,
        CancellationToken cancellationToken = default)
    {
        Attempts++;

        throw new InvalidOperationException("The broker is unreachable.");
    }
}
