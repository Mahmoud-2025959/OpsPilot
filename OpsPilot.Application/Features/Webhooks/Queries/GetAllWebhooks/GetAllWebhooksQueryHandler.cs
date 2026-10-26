using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Wrappers;
using OpsPilot.Contracts.Webhooks;

namespace OpsPilot.Application.Features.Webhooks.Queries.GetAllWebhooks;

public sealed class GetAllWebhooksQueryHandler
    : IRequestHandler<GetAllWebhooksQuery, PagedResponse<WebhookEndpointDto>>
{
    private readonly IWebhookRepository _webhooks;
    private readonly ISecretProtector _protector;

    public GetAllWebhooksQueryHandler(IWebhookRepository webhooks, ISecretProtector protector)
    {
        _webhooks = webhooks;
        _protector = protector;
    }

    public async Task<PagedResponse<WebhookEndpointDto>> Handle(
        GetAllWebhooksQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, total) = await _webhooks.GetPagedAsync(page, pageSize, cancellationToken);

        var dtos = items
            .Select(endpoint => endpoint.ToDto() with
            {
                // The list is where an operator reads back the secret they configured on the
                // other end, the same way the ingestion keys are read back. Endpoints
                // registered before the secret began being stored have none to read.
                Secret = _protector.Unprotect(endpoint.SecretCiphertext)
            })
            .ToList();

        return PagedResponse<WebhookEndpointDto>.Create(dtos, page, pageSize, total);
    }
}
