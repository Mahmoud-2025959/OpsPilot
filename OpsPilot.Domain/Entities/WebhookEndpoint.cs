using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// An outbound webhook registration for a tenant. Backs the Webhook notification channel
/// (ERD §3, "SLA والإشعارات").
/// </summary>
public sealed class WebhookEndpoint : TenantOwnedEntity
{
    private WebhookEndpoint(string url, string secretHash)
    {
        Url = url;
        SecretHash = secretHash;
    }

    /// <summary>Absolute HTTPS endpoint that receives the JSON payload.</summary>
    public string Url { get; private set; }

    /// <summary>
    /// SHA-256 hash of the shared signing secret. The secret itself is shown once at
    /// creation time and is never recoverable afterwards.
    /// </summary>
    public string SecretHash { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static WebhookEndpoint Create(Guid tenantId, string url, string secretHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretHash);

        return new WebhookEndpoint(Normalize(url), secretHash.Trim())
        {
            TenantId = tenantId
        };
    }

    /// <summary>Rotates the shared secret, e.g. after a suspected leak.</summary>
    public void RotateSecret(string newSecretHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSecretHash);
        SecretHash = newSecretHash.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Activate()
    {
        IsActive = true;
    }

    private static string Normalize(string url)
    {
        var trimmed = url.Trim();

        return Uri.TryCreate(trimmed, UriKind.Absolute, out _) ? trimmed : trimmed.ToLowerInvariant();
    }
}
