using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.IntegrationTests;

/// <summary>
/// A transport that refuses every call, so the dispatcher's retry rules can be watched.
/// </summary>
internal sealed class FailingWebhookSender : IWebhookSender
{
    private readonly bool _permanent;

    public FailingWebhookSender(bool permanent = false)
    {
        _permanent = permanent;
    }

    public Task SendAsync(WebhookDeliveryRequest request, CancellationToken cancellationToken = default) =>
        throw new WebhookDeliveryException("The endpoint refused the call.", _permanent);
}
