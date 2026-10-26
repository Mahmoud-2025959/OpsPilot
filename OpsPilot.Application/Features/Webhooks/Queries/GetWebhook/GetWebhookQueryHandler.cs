using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Webhooks;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Webhooks.Queries.GetWebhook;

public sealed class GetWebhookQueryHandler : IRequestHandler<GetWebhookQuery, WebhookEndpointDto>
{
    private readonly IWebhookRepository _webhooks;
    private readonly ISecretProtector _protector;

    public GetWebhookQueryHandler(IWebhookRepository webhooks, ISecretProtector protector)
    {
        _webhooks = webhooks;
        _protector = protector;
    }

    public async Task<WebhookEndpointDto> Handle(
        GetWebhookQuery request,
        CancellationToken cancellationToken)
    {
        // The lookup is tenant-filtered, so an endpoint belonging to another company is a miss
        // rather than something this caller can read.
        var endpoint = await _webhooks.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(WebhookEndpoint), request.Id);

        return endpoint.ToDto() with
        {
            Secret = _protector.Unprotect(endpoint.SecretCiphertext)
        };
    }
}
