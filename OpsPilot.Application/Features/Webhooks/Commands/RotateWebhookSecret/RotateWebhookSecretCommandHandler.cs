using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Webhooks;
using OpsPilot.Contracts.Webhooks;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Webhooks.Commands.RotateWebhookSecret;

public sealed class RotateWebhookSecretCommandHandler
    : IRequestHandler<RotateWebhookSecretCommand, CreatedWebhookDto>
{
    private readonly IWebhookRepository _webhooks;
    private readonly WebhookIssuer _issuer;
    private readonly IAuditLogWriter _audit;

    public RotateWebhookSecretCommandHandler(
        IWebhookRepository webhooks,
        WebhookIssuer issuer,
        IAuditLogWriter audit)
    {
        _webhooks = webhooks;
        _issuer = issuer;
        _audit = audit;
    }

    public async Task<CreatedWebhookDto> Handle(
        RotateWebhookSecretCommand request,
        CancellationToken cancellationToken)
    {
        var endpoint = await _webhooks.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new EntityNotFoundException(nameof(WebhookEndpoint), request.Id);

        var secret = await _issuer.RotateAsync(endpoint, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.WebhookSecretRotated,
                nameof(WebhookEndpoint),
                endpoint.Id.ToString()),
            cancellationToken);

        return new CreatedWebhookDto(
            endpoint.Id,
            endpoint.Url,
            secret,
            endpoint.CreatedAt);
    }
}
