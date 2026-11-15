using MediatR;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Contracts.Webhooks;

namespace OpsPilot.Application.Features.Webhooks.Queries.GetWebhookDeliveries;

/// <summary>
/// Lists the webhook calls the company has made, newest first.
/// </summary>
/// <param name="EndpointId">Only the calls made to this endpoint, or all of them when null.</param>
/// <param name="Page">The page to read, counted from one.</param>
/// <param name="PageSize">How many rows a page holds.</param>
public sealed record GetWebhookDeliveriesQuery(Guid? EndpointId, int Page, int PageSize)
    : IRequest<PagedResponse<WebhookDeliveryDto>>;
