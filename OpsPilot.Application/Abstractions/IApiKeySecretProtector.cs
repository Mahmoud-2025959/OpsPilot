namespace OpsPilot.Application.Abstractions;

/// <summary>
/// Puts the raw ingestion key behind a reversible cipher so it can be shown again later.
/// </summary>
/// <remarks>
/// <para>
/// The hash is what authentication uses and stays the reason a key cannot be read by anyone
/// who only has the database. This is the second copy: encrypted, so a database dump on its
/// own does not hand out usable credentials, but recoverable, so an operator who lost a key
/// can read it back instead of having to revoke and reissue it.
/// </para>
/// <para>
/// The application layer names the capability, not the algorithm. The cipher itself lives in
/// the infrastructure layer, which is the only place allowed to know about keys and
/// cryptography.
/// </para>
/// </remarks>
public interface IApiKeySecretProtector
{
    /// <summary>Encrypts a raw key for storage.</summary>
    string Protect(string rawKey);

    /// <summary>
    /// Decrypts a stored key. Returns null when there is nothing to decrypt, or when the
    /// ciphertext does not open with the configured key — a key issued before the column
    /// existed, or one encrypted with a key that has since been rotated away.
    /// </summary>
    string? Unprotect(string? ciphertext);
}
