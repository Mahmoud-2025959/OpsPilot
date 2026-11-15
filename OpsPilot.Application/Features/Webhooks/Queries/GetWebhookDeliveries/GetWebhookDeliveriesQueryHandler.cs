using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Contracts.Webhooks;

namespace OpsPilot.Application.Features.Webhooks.Queries.GetWebhookDeliveries;

public sealed class GetWebhookDeliveriesQueryHandler
    : IRequestHandler<GetWebhookDeliveriesQuery, PagedResponse<WebhookDeliveryDto>>
{
    private readonly IWebhookRepository _webhooks;

    public GetWebhookDeliveriesQueryHandler(IWebhookRepository webhooks)
    {
        _webhooks = webhooks;
    }

    public async Task<PagedResponse<WebhookDeliveryDto>> Handle(
        GetWebhookDeliveriesQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, total) = await _webhooks.GetDeliveriesPagedAsync(
            request.EndpointId,
            page,
            pageSize,
            cancellationToken);

        var dtos = items.Select(delivery => delivery.ToDto()).ToList();

        return PagedResponse<WebhookDeliveryDto>.Create(dtos, page, pageSize, total);
    }
}
