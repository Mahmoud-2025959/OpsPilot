using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// An outbound webhook registration for a tenant. Backs the Webhook notification channel
/// (ERD §3, "السلا والإشعارات").
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SecretHash"/> is a SHA-256 digest of the shared signing secret. The digest is
/// enough to answer "is this the secret we issued?" but not enough to sign anything, and
/// signing is the whole point of the secret — so the secret itself is kept as well, encrypted
/// (see <c>ISecretProtector</c>), and read back at each delivery.
/// </para>
/// <para>
/// The plaintext secret is shown once, when the endpoint is created or the secret is rotated.
/// It is recoverable afterwards from the stored copy, the same way an ingestion key is, so an
/// operator who lost it does not have to re-register the endpoint.
/// </para>
/// </remarks>
public sealed class WebhookEndpoint : TenantOwnedEntity
{
    private WebhookEndpoint(string url, string secretHash, string? secretCiphertext)
    {
        Url = url;
        SecretHash = secretHash;
        SecretCiphertext = secretCiphertext;
    }

    /// <summary>Absolute HTTPS endpoint that receives the JSON payload.</summary>
    public string Url { get; private set; }

    /// <summary>
    /// SHA-256 hash of the shared signing secret. The secret itself is shown once at
    /// creation time and is never recoverable afterwards.
    /// </summary>
    public string SecretHash { get; private set; }

    /// <summary>
    /// The signing secret, encrypted at rest. Null for rows created before the column
    /// existed: those endpoints cannot sign their deliveries and are parked until their
    /// secret is rotated.
    /// </summary>
    public string? SecretCiphertext { get; private set; }

    public bool IsActive { get; private set; } = true;

    public static WebhookEndpoint Create(
        Guid tenantId,
        string url,
        string secretHash,
        string? secretCiphertext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretHash);

        return new WebhookEndpoint(Normalize(url), secretHash.Trim(), secretCiphertext)
        {
            TenantId = tenantId
        };
    }

    /// <summary>Rotates the shared secret, e.g. after a suspected leak.</summary>
    public void RotateSecret(string newSecretHash, string? newSecretCiphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSecretHash);

        SecretHash = newSecretHash.Trim();
        SecretCiphertext = newSecretCiphertext;
    }

    /// <summary>Points the endpoint at a different destination.</summary>
    public void UpdateUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        Url = Normalize(url.Trim());
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
