using OpsPilot.Domain.Common;
using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Exceptions;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A machine credential used to ingest events, stored as a hash only.
/// </summary>
/// <remarks>
/// <see cref="KeyPrefix"/> is the first few characters of the key, kept in the clear so
/// that the UI can list keys and operators can tell them apart without exposing them.
/// </remarks>
public sealed class ApiKey : TenantOwnedEntity
{
    private ApiKey(string name, string keyPrefix, string keyHash, DateTimeOffset expiresAt)
    {
        Name = name;
        KeyPrefix = keyPrefix;
        KeyHash = keyHash;
        ExpiresAt = expiresAt;
    }

    public string Name { get; private set; }

    /// <summary>Short, non-secret leading segment of the key (ERD §3, "التدقيق والمفاتيح").</summary>
    public string KeyPrefix { get; private set; }

    /// <summary>SHA-256 hash of the key. The raw key is never persisted.</summary>
    public string KeyHash { get; private set; }

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
        DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new DomainException("API key expiration must be in the future.");
        }

        return new ApiKey(name.Trim(), keyPrefix.Trim(), keyHash.Trim(), expiresAt)
        {
            TenantId = tenantId
        };
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
