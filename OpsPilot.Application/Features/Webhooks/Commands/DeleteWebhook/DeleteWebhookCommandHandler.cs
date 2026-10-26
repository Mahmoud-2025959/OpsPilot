using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Webhooks.Commands.DeleteWebhook;

public sealed class DeleteWebhookCommandHandler : IRequestHandler<DeleteWebhookCommand>
{
    private readonly IWebhookRepository _webhooks;
    private readonly IAuditLogWriter _audit;

    public DeleteWebhookCommandHandler(IWebhookRepository webhooks, IAuditLogWriter audit)
    {
        _webhooks = webhooks;
        _audit = audit;
    }

    public async Task Handle(DeleteWebhookCommand request, CancellationToken cancellationToken)
    {
        var endpoint = await _webhooks.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(WebhookEndpoint), request.Id);

        await _webhooks.RemoveAsync(endpoint, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.WebhookDeleted,
                nameof(WebhookEndpoint),
                endpoint.Id.ToString(),
                OldValues: new { endpoint.Url, endpoint.IsActive }),
            cancellationToken);
    }
}
