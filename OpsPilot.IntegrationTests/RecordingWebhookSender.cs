using OpsPilot.Application.Abstractions;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// Captures what the dispatcher handed to the transport, for tests that drive a pass directly
/// instead of standing up a server on the other end.
/// </summary>
internal sealed class RecordingWebhookSender : IWebhookSender
{
    public List<WebhookDeliveryRequest> Sent { get; } = [];

    public Task SendAsync(WebhookDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        Sent.Add(request);
        return Task.CompletedTask;
    }
}
