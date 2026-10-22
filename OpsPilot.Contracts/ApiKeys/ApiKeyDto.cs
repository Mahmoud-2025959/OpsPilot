namespace OpsPilot.Contracts.ApiKeys;

/// <summary>
/// A key as it appears in the list. The secret is carried out in the clear because the list is
/// the view an operator uses to recover a key they lost (ERD §3, "التدقيق والمفاتيح").
/// </summary>
/// <remarks>
/// The endpoint that produces this needs the <c>apikeys.manage</c> permission, so it hands out
/// no more than the people who are allowed to issue and revoke keys already have. <see cref="Key"/>
/// is null for a key issued before the secret began being stored — a hash cannot be reversed
/// into the secret it was made from, so those rows have to be rotated before they have a value.
/// </remarks>
public sealed record ApiKeyDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string KeyPrefix,
    string Status,
    bool IsActive,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt,
    string? Key = null);

/// <summary>
/// The one and only time the raw key is returned. It is also returned when a key is rotated,
/// which is the way to recover the value of a key that predates the stored copy.
/// </summary>
public sealed record CreatedApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    string Key,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);
