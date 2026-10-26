namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Puts a raw secret behind a reversible cipher so it can be shown again later (keys and webhooks).
/// </summary>
/// <remarks>
/// <para>
/// For ingestion keys the hash is what authentication uses; the encrypted copy exists so the
/// raw key can be recovered for display. For webhooks the raw secret has to be recoverable to
/// sign each delivery — a hash cannot sign anything.
/// </para>
/// <para>
/// The application layer names the capability, not the algorithm. The cipher itself lives in
/// the infrastructure layer, which is the only place allowed to know about keys and
/// cryptography.
/// </para>
/// </remarks>
public interface ISecretProtector
{
    /// <summary>Encrypts a raw secret for storage.</summary>
    string Protect(string rawSecret);

    /// <summary>
    /// Decrypts a stored secret. Returns null when there is nothing to decrypt, or when the
    /// ciphertext does not open with the configured key — a secret stored before the column
    /// existed, or one encrypted with a key that has since been rotated away.
    /// </summary>
    string? Unprotect(string? ciphertext);
}
