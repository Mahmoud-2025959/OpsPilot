using OpsPilot.Application.Abstractions;
using OpsPilot.Domain.Entities;

namespace OpsPilot.Application.Features.Webhooks;

/// <summary>A freshly minted endpoint: the row that was saved and the secret behind it.</summary>
public sealed record IssuedWebhook(WebhookEndpoint Entity, string Secret);

/// <summary>
/// Registers webhook endpoints and rotates their signing secrets.
/// </summary>
/// <remarks>
/// Creating an endpoint and rotating its secret are the same three steps with a different
/// last one — insert a row or update the existing one — so they share this class rather than
/// each growing their own copy of the minting and storing.
/// </remarks>
public sealed class WebhookIssuer
{
    private readonly IWebhookRepository _repository;
    private readonly ISecretProtector _protector;

    public WebhookIssuer(IWebhookRepository repository, ISecretProtector protector)
    {
        _repository = repository;
        _protector = protector;
    }

    /// <summary>Registers an endpoint with a freshly minted secret, and saves it.</summary>
    public async Task<IssuedWebhook> IssueAsync(
        Guid tenantId,
        string url,
        CancellationToken cancellationToken = default)
    {
        var secret = WebhookSecrets.Generate();

        var endpoint = WebhookEndpoint.Create(
            tenantId,
            url,
            WebhookSecrets.Hash(secret),
            _protector.Protect(secret));

        await _repository.AddAsync(endpoint, cancellationToken);

        return new IssuedWebhook(endpoint, secret);
    }

    /// <summary>
    /// Mints a new secret for an endpoint that already exists. The previous secret stops being
    /// the one that signs deliveries as soon as this is saved.
    /// </summary>
    public async Task<string> RotateAsync(
        WebhookEndpoint endpoint,
        CancellationToken cancellationToken = default)
    {
        var secret = WebhookSecrets.Generate();

        endpoint.RotateSecret(WebhookSecrets.Hash(secret), _protector.Protect(secret));

        await _repository.UpdateAsync(endpoint, cancellationToken);

        return secret;
    }
}
