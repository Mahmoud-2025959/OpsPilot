using MediatR;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Contracts.Webhooks;

namespace OpsPilot.Application.Features.Webhooks.Queries.GetAllWebhooks;

/// <summary>Lists the webhook endpoints of the company, newest first.</summary>
public sealed record GetAllWebhooksQuery(int Page, int PageSize)
    : IRequest<PagedResponse<WebhookEndpointDto>>;
