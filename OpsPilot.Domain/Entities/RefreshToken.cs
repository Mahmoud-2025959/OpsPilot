using OpsPilot.Domain.Common;

namespace OpsPilot.Domain.Entities;

/// <summary>
/// A single-use refresh token, stored as a hash (ERD §3, "Identity و Tenancy").
/// </summary>
/// <remarks>
/// Rotation is recorded through <see cref="ReplacedByTokenId"/>, which chains each token
/// to its successor. Replaying an already-consumed token is therefore detectable, and
/// detection revokes the whole chain.
/// </remarks>
public sealed class RefreshToken : TenantOwnedEntity
{
    private RefreshToken(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        TenantId = tenantId;
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public bool IsUsed { get; private set; }

    public bool IsRevoked { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Token issued in exchange for this one, forming the rotation chain. Null while this
    /// token has not been exchanged.
    /// </summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActive => !IsUsed && !IsRevoked && ExpiresAt > DateTimeOffset.UtcNow;

    public static RefreshToken Create(Guid tenantId, Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken(tenantId, userId, tokenHash.Trim(), expiresAt);
    }

    /// <summary>
    /// Consumes this token and links it to its replacement. Re-consuming is a no-op so
    /// that a double submit does not corrupt the chain.
    /// </summary>
    public void MarkUsed(Guid replacedByTokenId)
    {
        if (IsUsed)
        {
            return;
        }

        IsUsed = true;
        ReplacedByTokenId = replacedByTokenId;
    }

    public void Revoke(DateTimeOffset at)
    {
        IsRevoked = true;
        RevokedAt ??= at;
    }
}
