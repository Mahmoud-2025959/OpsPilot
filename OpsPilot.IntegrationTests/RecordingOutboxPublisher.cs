using OpsPilot.Application.Abstractions;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Captures what the dispatcher handed to the outside world, for tests that drive a pass
/// directly instead of standing up a broker.
/// </summary>
internal sealed class RecordingOutboxPublisher : IOutboxPublisher
{
    public List<(string Type, string Payload)> Published { get; } = [];

    public Task PublishAsync(
        string type,
        string payload,
        CancellationToken cancellationToken = default)
    {
        Published.Add((type, payload));
        return Task.CompletedTask;
    }
}
