using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Common.Mappings;
using OpsPilot.Contracts.Webhooks;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Webhooks.Commands.UpdateWebhook;

public sealed class UpdateWebhookCommandHandler
    : IRequestHandler<UpdateWebhookCommand, WebhookEndpointDto>
{
    private readonly IWebhookRepository _webhooks;
    private readonly IAuditLogWriter _audit;

    public UpdateWebhookCommandHandler(IWebhookRepository webhooks, IAuditLogWriter audit)
    {
        _webhooks = webhooks;
        _audit = audit;
    }

    public async Task<WebhookEndpointDto> Handle(
        UpdateWebhookCommand request,
        CancellationToken cancellationToken)
    {
        // The lookup is tenant-filtered, so an endpoint belonging to another company is a miss
        // rather than something this caller can read.
        var endpoint = await _webhooks.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(WebhookEndpoint), request.Id);

        if (!string.Equals(endpoint.Url, request.Url, StringComparison.OrdinalIgnoreCase)
            && await _webhooks.UrlExistsAsync(request.Url, request.Id, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.WebhookAlreadyExists);
        }

        var oldUrl = endpoint.Url;
        var wasActive = endpoint.IsActive;

        endpoint.UpdateUrl(request.Url);

        if (endpoint.IsActive != request.IsActive)
        {
            if (request.IsActive)
            {
                endpoint.Activate();
            }
            else
            {
                endpoint.Deactivate();
            }
        }

        await _webhooks.UpdateAsync(endpoint, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.WebhookUpdated,
                nameof(WebhookEndpoint),
                endpoint.Id.ToString(),
                OldValues: new { oldUrl, wasActive },
                NewValues: new { endpoint.Url, endpoint.IsActive }),
            cancellationToken);

        return endpoint.ToDto();
    }
}
