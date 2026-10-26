using OpsPilot.Contracts.Webhooks;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Common.Mappings;

public static class WebhookMappings
{
    /// <summary>
    /// An endpoint as the API shows it. The signing secret is not part of this shape: it is
    /// carried by the endpoints that hand it out on purpose.
    /// </summary>
    public static WebhookEndpointDto ToDto(this WebhookEndpoint endpoint) =>
        new(endpoint.Id,
            endpoint.TenantId,
            endpoint.Url,
            endpoint.IsActive,
            endpoint.CreatedAt);

    public static WebhookDeliveryDto ToDto(this WebhookDelivery delivery) =>
        new(delivery.Id,
            delivery.EndpointId,
            delivery.EventType,
            delivery.Status.ToString(),
            delivery.Attempts,
            delivery.LastError,
            delivery.DeliveredAt,
            delivery.CreatedAt);
}
