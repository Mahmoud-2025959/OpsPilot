using MediatR;
using OpsPilot.Contracts.Webhooks;

namespace OpsPilot.Application.Features.Webhooks.Queries.GetWebhook;

/// <summary>Reads one webhook endpoint of the company.</summary>
public sealed record GetWebhookQuery(Guid Id) : IRequest<WebhookEndpointDto>;
