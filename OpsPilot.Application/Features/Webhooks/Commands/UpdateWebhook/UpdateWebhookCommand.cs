using MediatR;
using OpsPilot.Contracts.Webhooks;

namespace OpsPilot.Application.Features.Webhooks.Commands.UpdateWebhook;

/// <summary>
/// Points a registered endpoint somewhere else, or puts it in and out of service.
/// </summary>
/// <param name="Id">The endpoint to change.</param>
/// <param name="Url">The absolute URL that should receive the JSON payload.</param>
/// <param name="IsActive">Whether the endpoint should keep receiving deliveries.</param>
public sealed record UpdateWebhookCommand(Guid Id, string Url, bool IsActive)
    : IRequest<WebhookEndpointDto>;
