using System.Security.Cryptography;
using System.Text;

namespace OpsPilot.Application.Features.Webhooks;

/// <summary>
/// The lifecycle of a webhook's signing secret: mint it, name it, and hash it for storage.
/// </summary>
/// <remarks>
/// The secret is 256 bits of CSPRNG output, and the digest is a plain SHA-256 — no salt, no
/// stretching — for the same reason the ingestion keys use one: there is no low-entropy guess
/// to slow down, and the digest has to be recomputable on every delivery.
/// <para>
/// The secret itself is stored as well, encrypted (see <c>ISecretProtector</c>), because a
/// webhook signs with it and a hash cannot sign anything. That second copy is what lets the
/// plaintext be read back for the operator who configured the other end.
/// </para>
/// </remarks>
public static class WebhookSecrets
{
    private const string Scheme = "whsec_";

    /// <summary>Mints a fresh signing secret, e.g. <c>whsec_9f4c…</c>.</summary>
    public static string Generate() =>
        Scheme + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    /// <summary>The lookup value stored in place of the secret.</summary>
    public static string Hash(string rawSecret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawSecret))).ToLowerInvariant();
}
