using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A machine credential used to ingest events, stored as a hash and an encrypted copy of the secret.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KeyPrefix"/> is the first few characters of the key, kept in the clear so
/// that the UI can list keys and operators can tell them apart without exposing them.
/// </para>
/// <para>
/// <see cref="KeyHash"/> is what authentication looks the presented secret up by, and it is
/// enough on its own: the digest is recomputed on every request that presents a key.
/// <see cref="KeyCiphertext"/> is the same secret encrypted at rest, and it exists so the
/// value can be shown again later — an operator who lost a key would otherwise have to
/// revoke it and issue a new one. It is null for rows created before the column existed,
/// because a hash cannot be reversed into the secret it was made from.
/// </para>
/// </remarks>
public sealed class ApiKey : TenantOwnedEntity
{
    private ApiKey(string name, string keyPrefix, string keyHash, string? keyCiphertext, DateTimeOffset expiresAt)
    {
        Name = name;
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
        KeyCiphertext = keyCiphertext;
        ExpiresAt = expiresAt;
    }

    public string Name { get; private set; }

    /// <summary>Short, non-secret leading segment of the key (ERD §3, "التدقيق والمفاتيح").</summary>
    public string KeyPrefix { get; private set; }

    /// <summary>SHA-256 hash of the key. This is what authentication matches against.</summary>
    public string KeyHash { get; private set; }

    /// <summary>
    /// The raw key, encrypted. Null for keys issued before this column existed, and for a
    /// secret that could not be decrypted with the configured key.
    /// </summary>
    public string? KeyCiphertext { get; private set; }

    public ApiKeyStatus Status { get; private set; } = ApiKeyStatus.Active;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>Convenience view of <see cref="Status"/> (ERD models this as a boolean).</summary>
    public bool IsActive => IsUsable();

    public static ApiKey Create(
        Guid tenantId,
        string name,
        string keyPrefix,
        string keyHash,
        DateTimeOffset expiresAt,
        string? keyCiphertext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new DomainException("API key expiration must be in the future.");
        }

        return new ApiKey(name.Trim(), keyPrefix.Trim(), keyHash.Trim(), keyCiphertext, expiresAt)
        {
            TenantId = tenantId
        };
    }

    /// <summary>
    /// Replaces the secret on an existing key. The hash, the prefix and the encrypted copy move
    /// together, so the previous secret stops authenticating the moment this is saved.
    /// </summary>
    public void ReplaceSecret(string keyPrefix, string keyHash, string keyCiphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyCiphertext);

        KeyPrefix = keyPrefix.Trim();
        KeyHash = keyHash.Trim();
        KeyCiphertext = keyCiphertext;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    public void MarkUsed(DateTimeOffset at)
    {
        LastUsedAt = at;
    }

    public void Revoke()
    {
        Status = ApiKeyStatus.Revoked;
    }

    public void MarkExpired()
    {
        Status = ApiKeyStatus.Expired;
    }

    public bool IsUsable()
    {
        return Status == ApiKeyStatus.Active && ExpiresAt > DateTimeOffset.UtcNow;
    }
}
