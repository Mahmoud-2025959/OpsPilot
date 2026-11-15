using MediatR;
using OpsPilot.Application.Abstractions;
using OpsPilot.Application.Common.Constants;
using OpsPilot.Application.Features.Webhooks;
using OpsPilot.Contracts.Webhooks;
using OpsPilot.Domain.Entities;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Application.Features.Webhooks.Commands.CreateWebhook;

public sealed class CreateWebhookCommandHandler
    : IRequestHandler<CreateWebhookCommand, CreatedWebhookDto>
{
    private readonly IWebhookRepository _webhooks;
    private readonly WebhookIssuer _issuer;
    private readonly IAuditLogWriter _audit;

    public CreateWebhookCommandHandler(
        IWebhookRepository webhooks,
        WebhookIssuer issuer,
        IAuditLogWriter audit)
    {
        _webhooks = webhooks;
        _issuer = issuer;
        _audit = audit;
    }

    public async Task<CreatedWebhookDto> Handle(
        CreateWebhookCommand request,
        CancellationToken cancellationToken)
    {
        // The database enforces the same rule with a unique index on (TenantId, Url). Checking
        // first turns the would-be constraint violation into a readable conflict.
        if (await _webhooks.UrlExistsAsync(request.Url, null, cancellationToken))
        {
            throw new ConflictException(ExceptionMessages.WebhookAlreadyExists);
        }

        var issued = await _issuer.IssueAsync(request.TenantId, request.Url, cancellationToken);

        await _audit.WriteAsync(
            new AuditEntry(
                AuditActions.WebhookCreated,
                nameof(WebhookEndpoint),
                issued.Entity.Id.ToString(),
                NewValues: new { issued.Entity.Url }),
            cancellationToken);

        // The plaintext secret is handed back exactly once here. It stays recoverable from the
        // stored encrypted copy, which is what makes a lost secret a rotation rather than a
        // re-registration.
        return new CreatedWebhookDto(
            issued.Entity.Id,
            issued.Entity.Url,
            issued.Secret,
            issued.Entity.CreatedAt);
    }
}
