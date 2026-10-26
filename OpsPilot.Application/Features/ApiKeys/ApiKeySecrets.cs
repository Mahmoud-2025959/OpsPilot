using System.Security.Cryptography;
using System.Text;

namespace OpsPilot.Application.Features.ApiKeys;

/// <summary>
/// The lifecycle of an ingestion key's secret: mint it, name it, and hash it for storage.
/// </summary>
/// <remarks>
/// The hash is what authentication matches against, and it is the half that is irreversible:
/// the secret is 128 bits of CSPRNG output, so a plain SHA-256 — no salt, no stretching — is
/// the right tool, because there is no low-entropy guess to slow down and the digest has to
/// be recomputable on every request that presents a key.
/// <para>
/// The secret itself is stored as well, encrypted (see <c>IApiKeySecretProtector</c>), so that
/// a key an operator lost can be read back instead of having to be revoked and reissued. That
/// is a second copy of the secret, not a replacement for the hash: authentication still runs
/// entirely off the hash, and a database dump on its own yields no usable credential.
/// </para>
/// </remarks>
public static class ApiKeySecrets
{
    private const string Scheme = "op_live_";

    /// <summary>
    /// Characters of the raw key kept in the clear. Long enough to tell two keys apart in a list
    /// and to satisfy the column's 16-character limit, short enough to give nothing away.
    /// </summary>
    public const int PrefixLength = 16;

    /// <summary>Mints a fresh secret, e.g. <c>op_live_9f4c…</c>.</summary>
    public static string GenerateRawKey() =>
        Scheme + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    /// <summary>The non-secret leading segment stored alongside the hash so the UI can list keys.</summary>
    public static string PrefixOf(string rawKey) =>
        rawKey.Length <= PrefixLength ? rawKey : rawKey[..PrefixLength];

    /// <summary>The lookup value stored in place of the key.</summary>
    public static string Hash(string rawKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
}
