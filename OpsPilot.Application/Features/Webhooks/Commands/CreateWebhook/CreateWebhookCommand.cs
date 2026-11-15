using MediatR;
using OpsPilot.Contracts.Webhooks;

namespace OpsPilot.Application.Features.Webhooks.Commands.CreateWebhook;

/// <summary>
/// Registers a webhook endpoint and mints its signing secret.
/// </summary>
/// <param name="TenantId">The company the endpoint belongs to.</param>
/// <param name="Url">The absolute URL that receives the JSON payload.</param>
public sealed record CreateWebhookCommand(Guid TenantId, string Url) : IRequest<CreatedWebhookDto>;
